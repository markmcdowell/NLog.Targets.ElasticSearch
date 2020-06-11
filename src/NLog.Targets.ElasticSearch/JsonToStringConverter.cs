using System;
using Newtonsoft.Json;

namespace NLog.Targets.ElasticSearch
{
    /// <summary>Base Json to string converter</summary>
    public sealed class JsonToStringConverter : JsonConverter
    {
        private readonly Type _type;

        /// <inheritdoc />
        public override bool CanRead { get; } = false;

        public JsonToStringConverter(Type type)
        {
            _type = type;
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteValue(value.ToString());
            }
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException("Only serialization is supported");
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return _type.IsAssignableFrom(objectType);
        }
    }
}
