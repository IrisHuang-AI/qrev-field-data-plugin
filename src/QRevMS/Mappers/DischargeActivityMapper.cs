using System;
using System.Linq;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.ChannelMeasurements;
using FieldDataPluginFramework.DataModel.DischargeActivities;
using FieldDataPluginFramework.DataModel.Meters;
using FieldDataPluginFramework.DataModel.Verticals;
using FieldDataPluginFramework.Units;
using QRevMS.Schema;

namespace QRevMS.Mappers
{
    internal class DischargeActivityMapper
    {
        private Channel Channel { get; }
        private FieldVisitInfo FieldVisitInfo { get; }
        private TimeSpan UtcOffset { get; }

        public DischargeActivityMapper(Channel channel, FieldVisitInfo fieldVisitInfo, LocationInfo location)
        {
            Channel = channel ?? throw new ArgumentNullException(nameof(channel));
            FieldVisitInfo = fieldVisitInfo ?? throw new ArgumentNullException(nameof(fieldVisitInfo));
            UtcOffset = (location ?? throw new ArgumentNullException(nameof(location))).UtcOffset;
        }

        public DischargeActivity MapDischargeActivityDetails()
        {
            ValidateInternalMetricUnits();
            var unitSystem = CreateUnitSystem();

            var dischargeActivity = CreateDischargeActivityWithSummary(unitSystem);
            SetManualGaugingDischargeSection(dischargeActivity, unitSystem);

            return dischargeActivity;
        }

        private void ValidateInternalMetricUnits()
        {
            ThrowIfUnexpectedUnits("cms", nameof(Channel.ChannelSummary.ChannelTotalQ), Channel.ChannelSummary?.ChannelTotalQ?.unitsCode);
            ThrowIfUnexpectedUnits("m", nameof(Channel.ChannelSummary.ChannelWidth), Channel.ChannelSummary?.ChannelWidth?.unitsCode);
            ThrowIfUnexpectedUnits("sqm", nameof(Channel.ChannelSummary.ChannelArea), Channel.ChannelSummary?.ChannelArea?.unitsCode);
            ThrowIfUnexpectedUnits("mps", nameof(Channel.ChannelSummary.MeanNormalVelocity), Channel.ChannelSummary?.MeanNormalVelocity?.unitsCode);
        }

        private static void ThrowIfUnexpectedUnits(string expectedUnits, string name, string actualUnits)
        {
            if (string.IsNullOrEmpty(actualUnits))
                return;

            if (actualUnits != expectedUnits)
                throw new ArgumentException($"Expected units '{expectedUnits}' for {name} but found '{actualUnits}'");
        }

        private static UnitSystem CreateUnitSystem()
        {
            return new UnitSystem
            {
                DistanceUnitId = "m",
                AreaUnitId = "m^2",
                VelocityUnitId = "m/s",
                DischargeUnitId = "m^3/s"
            };
        }

        private DischargeActivity CreateDischargeActivityWithSummary(UnitSystem unitSystem)
        {
            var factory = new DischargeActivityFactory(unitSystem);

            var totalDischarge = Channel.ChannelSummary?.ChannelTotalQ?.Value ??
                                 throw new ArgumentException("No total discharge amount provided");

            var measurementPeriod = new DateTimeInterval(FieldVisitInfo.StartDate, FieldVisitInfo.EndDate);
            var dischargeActivity = factory.CreateDischargeActivity(measurementPeriod, Convert.ToDouble(totalDischarge));

            dischargeActivity.Party = FieldVisitInfo.Party;
            dischargeActivity.Comments = string.Join("\n", new[] 
                {
                    Channel.UserComment?.Value,
                    Channel.QA?.QRev_Message?.Value
                }
                .Where(comment => !string.IsNullOrWhiteSpace(comment))
                .Select(comment => comment.Trim()));

            return dischargeActivity;
        }

        private void SetManualGaugingDischargeSection(DischargeActivity dischargeActivity, UnitSystem unitSystem)
        {
            var manualGaugingDischargeSection = new ManualGaugingDischargeSection(
                dischargeActivity.MeasurementPeriod,
                ChannelMeasurementBaseConstants.DefaultChannelName,
                dischargeActivity.Discharge,
                unitSystem.DistanceUnitId,
                unitSystem.AreaUnitId,
                unitSystem.VelocityUnitId)
            {
                Party = dischargeActivity.Party,
                Comments = dischargeActivity.Comments,
                DischargeMethod = GetDischargeMethod(),
                NumberOfVerticals = Channel.VerticalDetails?.Length,
                StartPoint = GetStartPoint(),
                WidthValue = ConvertDecimalToDouble(Channel.ChannelSummary?.ChannelWidth?.Value),
                AreaValue = ConvertDecimalToDouble(Channel.ChannelSummary?.ChannelArea?.Value),
                VelocityAverageValue = ConvertDecimalToDouble(Channel.ChannelSummary?.MeanNormalVelocity?.Value)
            };

            AddVerticals(manualGaugingDischargeSection);

            dischargeActivity.ChannelMeasurements.Add(manualGaugingDischargeSection);

            dischargeActivity.QuantitativeUncertainty = ConvertDecimalToDouble(Channel.ChannelSummary?.Uncertainty?.Total?.Value);
            dischargeActivity.ActiveUncertaintyType = dischargeActivity.QuantitativeUncertainty.HasValue
                ? UncertaintyType.Quantitative
                : UncertaintyType.None;
        }

        private void AddVerticals(ManualGaugingDischargeSection dischargeSection)
        {
            var verticals = Channel.VerticalDetails ?? Array.Empty<ChannelVertical>();

            for (var index = 0; index < verticals.Length; index++)
            {
                var vertical = verticals[index];

                var velocityObservation = new VelocityObservation
                {
                    MeterCalibration = new MeterCalibration
                    {
                        MeterType = MeterType.Adcp,
                        SerialNumber = Channel.Instrument?.SerialNumber?.Value, // required
                        Model = Channel.Instrument?.Model?.Value, // required
                        Manufacturer = Channel.Instrument?.Manufacturer?.Value, // required
                        FirmwareVersion = Channel.Instrument?.FirmwareVersion?.Value,
                        SoftwareVersion = Channel.QRevVersion
                    },
                    VelocityObservationMethod = PointVelocityObservationType.OneAtPointFive,
                    MeanVelocity = Convert.ToDouble(vertical.NormalVelocity?.Value), // required
                    Observations =
                    {
                        new VelocityDepthObservation
                        {
                            Velocity = Convert.ToDouble(vertical.NormalVelocity?.Value)
                        }
                    }
                };

                var measurementConditionData = GetMeasurementConditionData(index);
                var soundedDepth = Convert.ToDouble(vertical.Depth?.Value);

                var verticalDischarge = new Vertical
                {
                    SequenceNumber = index + 1,
                    MeasurementConditionData = measurementConditionData,
                    VelocityObservation = velocityObservation,
                    VerticalType = GetVerticalType(index, verticals.Length),
                    FlowDirection = FlowDirectionType.Normal,
                    TaglinePosition = Convert.ToDouble(vertical.Location?.Value), // required
                    SoundedDepth = soundedDepth, // required
                    EffectiveDepth = GetEffectiveDepth(soundedDepth, measurementConditionData),
                    MeasurementTime = ParseVerticalMeasurementDateTime(vertical.StartDateTime?.Value),
                    Segment = GetSegment(index, dischargeSection.DischargeMethod)
                };

                dischargeSection.Verticals.Add(verticalDischarge);
            }
        }

        private DischargeMethodType GetDischargeMethod()
        {
            var dischargeMethod = Channel.Processing?.DischargeMethod?.Value; // required
            
            if (string.Equals(dischargeMethod, "Mean-Section", StringComparison.OrdinalIgnoreCase))
                return DischargeMethodType.MeanSection;

            if (string.Equals(dischargeMethod, "Mid-Section", StringComparison.OrdinalIgnoreCase))
                return DischargeMethodType.MidSection;
            
            throw new ArgumentException($"Unsupported discharge method '{dischargeMethod}'. Expected 'Mean-Section' or 'Mid-Section'.");
        }

        private StartPointType GetStartPoint()
        {
            var startingBank = Channel.VerticalDetails[0]?.Bank?.Value;
            
            return string.Equals(startingBank, "Right", StringComparison.OrdinalIgnoreCase)
                ? StartPointType.RightEdgeOfWater
                : StartPointType.LeftEdgeOfWater;
        }

        private MeasurementConditionData GetMeasurementConditionData(int index)
        {
            var waterSurfaceCondition = Channel.VerticalDetails[index]?.WaterSurfaceCondition?.Value
                ?? Channel.Processing?.WaterSurfaceCondition?.Value;

            if (string.Equals(waterSurfaceCondition, "Open", StringComparison.OrdinalIgnoreCase))
                return new OpenWaterData();

            if (string.Equals(waterSurfaceCondition, "Ice", StringComparison.OrdinalIgnoreCase))
                return new IceCoveredData
                {
                    IceThickness = Convert.ToDouble(Channel.VerticalDetails[index]?.IceThickness.Value),
                    WaterSurfaceToBottomOfSlush = Convert.ToDouble(Channel.VerticalDetails[index]?.Depth2SlushBottom.Value),
                    WaterSurfaceToBottomOfIce = Convert.ToDouble(Channel.VerticalDetails[index]?.Depth2IceBottom.Value)
                };

            throw new ArgumentException($"Unsupported measurement condition type '{waterSurfaceCondition}'. Expected 'Open' or 'Ice'.");
        }
        
        private static double GetEffectiveDepth(double soundedDepth, MeasurementConditionData measurementConditionData)
        {
            if (measurementConditionData is IceCoveredData iceCoveredData)
                return soundedDepth - iceCoveredData.WaterSurfaceToBottomOfSlush;

            return soundedDepth;
        }

        private static VerticalType GetVerticalType(int index, int total)
        {
            if (index == 0)
            {
                return VerticalType.StartEdgeNoWaterBefore;
            }
            
            if (index == total - 1)
            {
                return VerticalType.EndEdgeNoWaterAfter;
            }

            return VerticalType.MidRiver;
        }
        
        private Segment GetSegment(int verticalIndex, DischargeMethodType dischargeMethod)
        {
            var stationIndex = dischargeMethod == DischargeMethodType.MeanSection
                ? verticalIndex - 1
                : verticalIndex;

            if (stationIndex < 0)
                return null;
            
            return new Segment
            {
                Width = Convert.ToDouble(Channel.StationDischarge[stationIndex]?.StationWidth.Value),
                Area = Convert.ToDouble(Channel.StationDischarge[stationIndex]?.StationArea.Value),
                Velocity = Convert.ToDouble(Channel.StationDischarge[stationIndex]?.StationNormalVelocity.Value),
                Discharge = Convert.ToDouble(Channel.StationDischarge[stationIndex]?.StationTotalQ.Value),
                TotalDischargePortion = Convert.ToDouble(Channel.StationDischarge[stationIndex]?.StationPercentQ.Value)
            };
        }

        private DateTimeOffset? ParseVerticalMeasurementDateTime(string dateTimeString)
        {
            if (string.IsNullOrWhiteSpace(dateTimeString))
                return null;

            if (DateTime.TryParseExact(dateTimeString, "yyyy.MM.dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var dateTime))
            {
                return new DateTimeOffset(dateTime, UtcOffset);
            }

            return null;
        }
        
        private double? ConvertDecimalToDouble(decimal? value)
        {
            return value.HasValue ? Convert.ToDouble(value.Value) : (double?)null;
        }
    }
}
