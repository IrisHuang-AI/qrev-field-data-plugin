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
            var channel = XmlDeserializer.DeserializeNoThrow<Channel>(fileStream, logger);

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
                logger.Error($"Something went wrong: {exception.Message}\n{exception.StackTrace}");
                return ParseFileResult.SuccessfullyParsedButDataInvalid(exception);
            }
        }

        public ParseFileResult ParseFile(Stream fileStream, LocationInfo targetLocation, IFieldDataResultsAppender appender, ILog logger)
        {
            var xmlRoot = XmlDeserializer.DeserializeNoThrow<Channel>(fileStream, logger);

            return xmlRoot == null 
                ? ParseFileResult.CannotParse() 
                : ParseXmlRootNoThrow(targetLocation, xmlRoot, appender, logger);
        }
    }
}
