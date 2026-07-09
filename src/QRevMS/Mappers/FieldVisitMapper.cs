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
            UtcOffset = location.UtcOffset;
        }

        public FieldVisitDetails MapFieldVisitDetails()
        {
            var visitPeriod = GetVisitTimePeriod();
            return new FieldVisitDetails(visitPeriod);
        }

        private DateTimeInterval GetVisitTimePeriod()
        {
            var times = (Channel.VerticalDetails ?? Array.Empty<ChannelVertical>())
                .SelectMany(v => new[] { v.StartDateTime?.Value, v.EndDateTime?.Value })
                .Select(ParseDateTime)
                .Where(dt => dt.HasValue)
                .Select(dt => dt.Value)
                .OrderBy(dt => dt)
                .ToList();

            return !times.Any() 
                ? throw new ArgumentException("Can't parse any timestamps from the verticals") 
                : new DateTimeInterval(times.First(), times.Last());
        }

        private DateTimeOffset? ParseDateTime(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;
            
            if (DateTime.TryParseExact(s, "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.NoCurrentDateDefault | DateTimeStyles.AllowWhiteSpaces, out var dateTime))
            {
                return new DateTimeOffset(dateTime, UtcOffset);
            }

            return null;
        }
    }
}

