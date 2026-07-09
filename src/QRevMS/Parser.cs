using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.Results;
using QRevMS.Mappers;
using QRevMS.Schema;

namespace QRevMS
{
    public class Parser
    {
        private LocationInfo Location { get; }
        private IFieldDataResultsAppender Appender { get; }
        private ILog Logger { get; }

        public Parser(LocationInfo location, IFieldDataResultsAppender appender, ILog logger)
        {
            Location = location;
            Appender = appender;
            Logger = logger;
        }

        public void Parse(Channel channel)
        {
            var fieldVisitInfo = AppendMappedFieldVisitInfo(channel, Location);

            AppendMappedMeasurements(channel, fieldVisitInfo);
        }

        private FieldVisitInfo AppendMappedFieldVisitInfo(Channel channel, LocationInfo locationInfo)
        {
            var mapper = new FieldVisitMapper(channel, Location);
            var fieldVisitDetails = mapper.MapFieldVisitDetails();

            Logger.Info($"Successfully parsed one visit '{fieldVisitDetails.FieldVisitPeriod}' for location '{locationInfo.LocationIdentifier}'");

            return Appender.AddFieldVisit(locationInfo, fieldVisitDetails);
        }

        private void AppendMappedMeasurements(Channel channel, FieldVisitInfo fieldVisitInfo)
        {
            var config = new ConfigLoader(Appender).Load();

            var dischargeActivityMapper = new DischargeActivityMapper(config, fieldVisitInfo);

            Appender.AddDischargeActivity(fieldVisitInfo, dischargeActivityMapper.Map(channel));
        }
    }
}
