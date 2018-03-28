using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace NLog.Targets.ElasticSearch
{
    public static class ExpandoObjectExtensions
    {
        /// <summary>
        /// Replaces dot ('.') character in Keys with an underscore ('_') 
        /// </summary>
        /// <returns>ExpandoObject</returns>
        public static ExpandoObject ReplaceDotInKeys(this ExpandoObject obj)
        {
            var clone = new ExpandoObject();
            foreach (var item in obj)
            {
                switch (item.Value)
                {
                    case null:
                        continue;
                    case ExpandoObject expandoObject:
                        ((IDictionary<string, object>)clone)[item.Key.Replace('.', '_')] = expandoObject.ReplaceDotInKeys();
                        break;
                    default:
                        if (item.Key.Contains('.'))
                            ((IDictionary<string, object>)clone)[item.Key.Replace('.', '_')] = item.Value;
                        else
                            ((IDictionary<string, object>)clone)[item.Key] = item.Value;
                        break;
                }
            }
            return clone;
        }
    }
}
