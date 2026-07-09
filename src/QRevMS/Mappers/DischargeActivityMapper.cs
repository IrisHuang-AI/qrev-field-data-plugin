using System;
using System.Globalization;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.ChannelMeasurements;
using FieldDataPluginFramework.DataModel.DischargeActivities;
using FieldDataPluginFramework.Units;
using QRevMS.Schema;

namespace QRevMS.Mappers
{
    internal class DischargeActivityMapper
    {
        private FieldVisitInfo FieldVisitInfo { get; }
        private Config Config { get; }

        private UnitConverter UnitConverter { get; set; }

        public DischargeActivityMapper(Config config, FieldVisitInfo fieldVisitInfo)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            FieldVisitInfo = fieldVisitInfo ?? throw new ArgumentNullException(nameof(fieldVisitInfo));
        }

        public DischargeActivity Map(Channel channel)
        {
            ValidateInternalMetricUnits(channel);

            UnitConverter = new UnitConverter(Config.ImperialUnits);

            var unitSystem = CreateUnitSystem();

            var dischargeActivity = CreateDischargeActivityWithSummary(channel, unitSystem);

            SetDischargeSection(dischargeActivity, channel, unitSystem);

            return dischargeActivity;
        }

        private void ValidateInternalMetricUnits(Channel channel)
        {
            ThrowIfUnexpectedUnits("cms", nameof(channel.ChannelSummary.ChannelTotalQ), channel.ChannelSummary?.ChannelTotalQ?.unitsCode);
            ThrowIfUnexpectedUnits("m", nameof(channel.ChannelSummary.ChannelWidth), channel.ChannelSummary?.ChannelWidth?.unitsCode);
            ThrowIfUnexpectedUnits("sqm", nameof(channel.ChannelSummary.ChannelArea), channel.ChannelSummary?.ChannelArea?.unitsCode);
            ThrowIfUnexpectedUnits("mps", nameof(channel.ChannelSummary.MeanNormalVelocity), channel.ChannelSummary?.MeanNormalVelocity?.unitsCode);
        }

        private void ThrowIfUnexpectedUnits(string expectedUnits, string name, string actualUnits)
        {
            if (string.IsNullOrEmpty(actualUnits))
                return;

            if (actualUnits != expectedUnits)
                throw new ArgumentException($"Expected units '{expectedUnits}' for {name} but found '{actualUnits}'");
        }

        private UnitSystem CreateUnitSystem()
        {
            UnitConverter = new UnitConverter(Config.ImperialUnits);

            return new UnitSystem
            {
                DistanceUnitId = UnitConverter.GetDistanceUnitId(),
                AreaUnitId = UnitConverter.GetAreaUnitId(),
                VelocityUnitId = UnitConverter.GetVelocityUnitId(),
                DischargeUnitId = UnitConverter.GetDischargeUnitId(),
            };
        }

        private DischargeActivity CreateDischargeActivityWithSummary(Channel channel, UnitSystem unitSystem)
        {
            var factory = new DischargeActivityFactory(unitSystem);

            var totalDischargeValue = ParseDoubleValue(channel.ChannelSummary?.ChannelTotalQ?.Value);
            if (!totalDischargeValue.HasValue)
                throw new ArgumentException("No total discharge amount provided");

            var measurementPeriod = GetMeasurementPeriod();
            var dischargeActivity = factory.CreateDischargeActivity(
                measurementPeriod,
                totalDischargeValue.Value);

            dischargeActivity.Comments = channel.QA?.QRev_Message?.Value ?? string.Empty;

            return dischargeActivity;
        }

        private DateTimeInterval GetMeasurementPeriod()
        {
            return new DateTimeInterval(FieldVisitInfo.StartDate, FieldVisitInfo.EndDate);
        }

        private void SetDischargeSection(DischargeActivity dischargeActivity, Channel channel, UnitSystem unitSystem)
        {
            var dischargeSection = CreateDischargeSectionWithDescription(dischargeActivity, channel, unitSystem);

            dischargeActivity.ChannelMeasurements.Add(dischargeSection);

            var uncertaintyValue = ParseDoubleValue(channel.ChannelSummary?.Uncertainty?.Total?.Value);
            dischargeActivity.QuantitativeUncertainty = uncertaintyValue;
            dischargeActivity.ActiveUncertaintyType = dischargeActivity.QuantitativeUncertainty.HasValue
                ? UncertaintyType.Quantitative
                : UncertaintyType.None;
        }

        private AdcpDischargeSection CreateDischargeSectionWithDescription(DischargeActivity dischargeActivity,
            Channel channel, UnitSystem unitSystem)
        {
            var middleDischarge = ParseDoubleValue(channel.ChannelSummary?.ChannelMiddleQ?.Value);
            var totalDischarge = dischargeActivity.Discharge.Value;

            var percentOfDischargeMeasured = middleDischarge.HasValue
                ? (double?)(100.0 * middleDischarge.Value / totalDischarge)
                : null;

            var adcpDischargeSection = new AdcpDischargeSection(
                dischargeActivity.MeasurementPeriod,
                ChannelMeasurementBaseConstants.DefaultChannelName,
                dischargeActivity.Discharge,
                $"{channel.Instrument?.Manufacturer?.Value} {channel.Instrument?.Model?.Value}",
                unitSystem.DistanceUnitId,
                unitSystem.AreaUnitId,
                unitSystem.VelocityUnitId)
            {
                Party = dischargeActivity.Party,
                Comments = dischargeActivity.Comments,
                WidthValue = ParseDoubleValue(channel.ChannelSummary?.ChannelWidth?.Value),
                AreaValue = ParseDoubleValue(channel.ChannelSummary?.ChannelArea?.Value),
                VelocityAverageValue = ParseDoubleValue(channel.ChannelSummary?.MeanNormalVelocity?.Value),
                PercentOfDischargeMeasured = percentOfDischargeMeasured,
                SoftwareVersion = channel.QRevVersion,
                FirmwareVersion = channel.Instrument?.FirmwareVersion?.Value,
                MeasurementDevice = new MeasurementDevice(
                    channel.Instrument?.Manufacturer?.Value,
                    channel.Instrument?.Model?.Value,
                    channel.Instrument?.SerialNumber?.Value),
            };

            return adcpDischargeSection;
        }

        private double? ParseDoubleValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;

            return null;
        }
    }
}
