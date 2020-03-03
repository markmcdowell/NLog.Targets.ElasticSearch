using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace NLog.Targets.ElasticSearch
{
    /// <summary>
    /// Serializes all non-simple properties as object.ToString()
    /// </summary>
    internal sealed class FlatObjectContractResolver : DefaultContractResolver
    {
        private readonly FlatObjectConverter _flatObjectConverter = new FlatObjectConverter();

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var jsonProperty = base.CreateProperty(member, memberSerialization);
            if (jsonProperty.Readable && !IsSimpleType(jsonProperty.PropertyType))
                jsonProperty.Converter = _flatObjectConverter;
            return jsonProperty;
        }

        private static bool IsSimpleType(Type propertyType)
        {
            return propertyType != null && (Type.GetTypeCode(propertyType) != TypeCode.Object || propertyType.IsValueType);
        }

        private class FlatObjectConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return true;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return null;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                }
                else if (value is System.Collections.IEnumerable)
                {
                    writer.WriteNull();
                }
                else
                {
                    writer.WriteValue(value.ToString());
                }
            }
        }
    }
}
