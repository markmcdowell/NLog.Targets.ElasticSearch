using System;
using System.Collections.Generic;
using System.Configuration;
using System.Dynamic;
using System.Linq;

namespace NLog.Targets.ElasticSearch
{
    internal static class Extensions
    {
        public static object ToSystemType(this string field, Type type)
        {
            switch (type.FullName)
            {
                case "System.Boolean":
                    return Convert.ToBoolean(field);
                case "System.Double":
                    return Convert.ToDouble(field);
                case "System.DateTime":
                    return Convert.ToDateTime(field);
                case "System.Int32":
                    return Convert.ToInt32(field);
                case "System.Int64":
                    return Convert.ToInt64(field);
                default:
                    return field;
            }
        }

        public static string GetConnectionString(this string name)
        {
            var value = GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value))
                return value;

            var connectionString = ConfigurationManager.ConnectionStrings[name];

            return connectionString?.ConnectionString;
        }

        /// <summary>
        /// Replaces dot ('.') character in Keys with an underscore ('_') 
        /// </summary>
        /// <returns>ExpandoObject</returns>
        public static ExpandoObject ReplaceDotInKeys(this ExpandoObject obj)
        {
            var clone = new ExpandoObject();
            foreach (var item in obj)
            {
                if (item.Value == null) continue;

                if (item.Value.GetType() == typeof(ExpandoObject))
                    ((IDictionary<string, object>)clone)[item.Key.Replace('.', '_')]
                        = (item.Value as ExpandoObject).ReplaceDotInKeys();
                else if (item.Key.Contains('.'))
                    ((IDictionary<string, object>)clone)[item.Key.Replace('.', '_')] = item.Value;
                else
                    ((IDictionary<string, object>)clone)[item.Key] = item.Value;
            }
            return clone;
        }

        private static string GetEnvironmentVariable(this string name)
        {
            return string.IsNullOrEmpty(name) ? null : Environment.GetEnvironmentVariable(name);
        }
    }
}