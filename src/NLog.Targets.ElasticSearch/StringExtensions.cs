using System;
using System.Dynamic;
using System.Globalization;
using Newtonsoft.Json;
#if NET45
using System.Configuration;
#else
using Microsoft.Extensions.Configuration;
#endif
using System.IO;

namespace NLog.Targets.ElasticSearch
{
    internal static class StringExtensions
    {
        public static object ToSystemType(this string field, Type type, IFormatProvider formatProvider, JsonSerializer jsonSerializer)
        {
            if (formatProvider == null)
                formatProvider = CultureInfo.CurrentCulture;

            switch (type.FullName)
            {
                case "System.Boolean":
                    return Convert.ToBoolean(field, formatProvider);
                case "System.Double":
                    return Convert.ToDouble(field, formatProvider);
                case "System.DateTime":
                    return Convert.ToDateTime(field, formatProvider);
                case "System.Int32":
                    return Convert.ToInt32(field, formatProvider);
                case "System.Int64":
                    return Convert.ToInt64(field, formatProvider);
                case "System.Object":
                    using (JsonTextReader reader = new JsonTextReader(new StringReader(field)))
                    {
                        return ((ExpandoObject)jsonSerializer.Deserialize(reader, typeof(ExpandoObject))).ReplaceDotInKeys(alwaysCloneObject: false);
                    }
                default:
                    return field;
            }
        }

        public static string GetConnectionString(this string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value))
                return value;

#if NET45
            var connectionString = ConfigurationManager.ConnectionStrings[name];
            return connectionString?.ConnectionString;
#else
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true);

            string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (!string.IsNullOrEmpty(env))
                builder.AddJsonFile($"appsettings.{env}.json", true, true);

            var configuration = builder.Build();

            return configuration.GetConnectionString(name);
#endif
        }
    }
}