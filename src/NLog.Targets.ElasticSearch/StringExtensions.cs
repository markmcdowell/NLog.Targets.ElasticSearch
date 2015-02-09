using System;

namespace NLog.Targets.ElasticSearch
{
    public static class StringExtensions
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
    }
}