using System;
using System.Globalization;
using System.Linq;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using QRevMS.Schema;

namespace QRevMS.Mappers
{
    public class FieldVisitMapper
    {
        private Channel Channel { get; }
        private TimeSpan UtcOffset { get; }

        public FieldVisitMapper(Channel channel, LocationInfo location)
        {
            Channel = channel ?? throw new ArgumentNullException(nameof(channel));
            UtcOffset = (location ?? throw new ArgumentNullException(nameof(location))).UtcOffset;
        }

        public FieldVisitDetails MapFieldVisitDetails()
        {
            var visitPeriod = GetVisitTimePeriod();
            var party = Channel.SiteInformation?.Persons?.Value;

            return new FieldVisitDetails(visitPeriod)
            {
                Party = party
            };
        }

        private DateTimeInterval GetVisitTimePeriod()
        {
            var times = (Channel.VerticalDetails ?? Array.Empty<ChannelVertical>())
                .SelectMany(vertical => new[] { vertical.StartDateTime?.Value, vertical.EndDateTime?.Value })
                .Select(ParseDateTime)
                .Where(dateTime => dateTime.HasValue)
                .Select(dateTime => dateTime.Value)
                .OrderBy(dateTime => dateTime)
                .ToList();

            if (!times.Any())
                throw new ArgumentException($"Can't parse any timestamps from the verticals");

            return new DateTimeInterval(times.First(), times.Last());
        }

        private DateTimeOffset? ParseDateTime(string dateTimeString)
        {
            return DateTime.TryParseExact(dateTimeString, "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dateTime)
                ? (DateTimeOffset?)new DateTimeOffset(dateTime, UtcOffset)
                : null;
        }
    }
}
