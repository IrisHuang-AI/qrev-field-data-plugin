using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using FieldDataPluginFramework.Results;
using ServiceStack;

namespace QRevMS
{
    public class ConfigLoader
    {
        private Dictionary<string,string> Settings { get; }

        public ConfigLoader(IFieldDataResultsAppender appender)
        {
            Settings = appender.GetPluginConfigurations();
        }

        public Config Load()
        {
            if (!Settings.TryGetValue(nameof(Config), out var jsonText) || string.IsNullOrWhiteSpace(jsonText))
                return new Config();

            try
            {
                return jsonText.FromJson<Config>();
            }
            catch (SerializationException exception)
            {
                throw new ArgumentException($"Invalid Config JSON:\b{jsonText}", exception);
            }
        }
    }
}
