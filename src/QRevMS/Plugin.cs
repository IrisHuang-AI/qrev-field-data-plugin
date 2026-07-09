using System;
using System.IO;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.Results;
using QRevMS.Schema;

namespace QRevMS
{
    public class Plugin : IFieldDataPlugin
    {
        public ParseFileResult ParseFile(Stream fileStream, IFieldDataResultsAppender appender, ILog logger)
        {
            var channel = QRevMSSerializer.DeserializeNoThrow(fileStream, logger);

            if (channel == null)
            {
                return ParseFileResult.CannotParse();
            }

            var locationIdentifier = channel.SiteInformation?.SiteID?.Value;

            if (string.IsNullOrWhiteSpace(locationIdentifier))
            {
                logger.Error("File can be parsed but there is no SiteID specified.");
                return ParseFileResult.SuccessfullyParsedButDataInvalid("Missing <Channel/SiteInformation/SiteID>");
            }

            try
            {
                var trimmedLocationIdentifier = locationIdentifier.Trim();
                var location = appender.GetLocationByIdentifier(trimmedLocationIdentifier);

                return ParseXmlRootNoThrow(location, channel, appender, logger);
            }
            catch (Exception exception)
            {
                logger.Error($"Cannot find location with identifier '{locationIdentifier}'.");
                return ParseFileResult.CannotParse(exception);
            }
        }

        public ParseFileResult ParseFile(Stream fileStream, LocationInfo targetLocation, IFieldDataResultsAppender appender, ILog logger)
        {
            var xmlRoot = QRevMSSerializer.DeserializeNoThrow(fileStream, logger);

            if (xmlRoot == null)
                return ParseFileResult.CannotParse();

            return ParseXmlRootNoThrow(targetLocation, xmlRoot, appender, logger);
        }

        private ParseFileResult ParseXmlRootNoThrow(LocationInfo location, Channel channel, IFieldDataResultsAppender appender, ILog logger)
        {
            try
            {
                var parser = new Parser(location, appender, logger);

                parser.Parse(channel);

                return ParseFileResult.SuccessfullyParsedAndDataValid();
            }
            catch (Exception exception)
            {
                logger.Error($"Error parsing file: {exception}");
                return ParseFileResult.SuccessfullyParsedButDataInvalid(exception);
            }
        }
    }
}
