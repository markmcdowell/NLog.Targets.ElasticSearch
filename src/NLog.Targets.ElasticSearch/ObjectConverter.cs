using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NLog.Targets.ElasticSearch
{
    internal static class ObjectConverter
    {
        public static object FormatValueSafe(object value, int maxRecursionLimit, JsonSerializer jsonSerializer)
        {
            if (value is Exception)
            {
                return FormatToExpandoObject(value, jsonSerializer);
            }
            else if (maxRecursionLimit >= 0)
            {
                if (Convert.GetTypeCode(value) != TypeCode.Object || value.GetType().IsValueType)
                {
                    return value;
                }
                else if (maxRecursionLimit == 0)
                {
                    if (value is System.Collections.IEnumerable)
                        return null;
                    else
                        return value.ToString();
                }
                else if (jsonSerializer.ContractResolver.ResolveContract(value.GetType()) is Newtonsoft.Json.Serialization.JsonObjectContract)
                {
                    return FormatToExpandoObject(value, jsonSerializer);
                }
            }

            return value;
        }

        private static object FormatToExpandoObject(object value, JsonSerializer jsonSerializer)
        {
            string field = SerializeToJson(value, jsonSerializer);
            var expandoObject = field.ToExpandoObject(jsonSerializer);
            if (value is Exception && expandoObject is IDictionary<string, object> dictionary)
            {
                dictionary["Type"] = value.GetType().ToString();
            }
            return expandoObject;
        }

        private static string SerializeToJson(object value, JsonSerializer jsonSerializer)
        {
            var sb = new System.Text.StringBuilder(256);
            var sw = new System.IO.StringWriter(sb, System.Globalization.CultureInfo.InvariantCulture);
            using (JsonTextWriter jsonWriter = new JsonTextWriter(sw))
            {
                jsonWriter.Formatting = jsonSerializer.Formatting;
                jsonSerializer.Serialize(jsonWriter, value, value.GetType());
            }
            return sb.ToString();
        }
    }
}
