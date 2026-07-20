using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using FieldDataPluginFramework;

namespace QRevMS
{
    public class XmlDeserializer
    {
        public static T DeserializeNoThrow<T>(Stream stream, ILog logger) where T : class
        {
            try
            {
                using (var streamReader = new StreamReader(stream, Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true))
                {
                    var xmlString = streamReader.ReadToEnd();
                    stream.Position = 0;

                    var serializer = new XmlSerializer(typeof(T));
                    var memoryStream = new MemoryStream((new UTF8Encoding()).GetBytes(xmlString));

                    return serializer.Deserialize(memoryStream) as T;
                }
            }
            catch (Exception exception)
            {
                logger.Error($"Deserialization failed: {exception}");
                return null;
            }
        }
    }
}
