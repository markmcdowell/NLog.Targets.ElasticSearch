using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace NLog.Targets.ElasticSearch
{
    internal static class ExpandoObjectExtensions
    {
        /// <summary>
        /// Replaces dot ('.') character in Keys with an underscore ('_') 
        /// </summary>
        /// <returns>ExpandoObject</returns>
        public static ExpandoObject ReplaceDotInKeys(this ExpandoObject obj, bool alwaysCloneObject = true)
        {
            var clone = alwaysCloneObject ? new ExpandoObject() : null;
            foreach (var item in obj)
            {
                switch (item.Value)
                {
                    case null:
                        if (clone == null)
                            return obj.ReplaceDotInKeys();
                        break;
                    case ExpandoObject expandoObject:
                        if (clone == null)
                            return obj.ReplaceDotInKeys();
                        ((IDictionary<string, object>)clone)[item.Key.Replace('.', '_')] = expandoObject.ReplaceDotInKeys();
                        break;
                    default:
                        if (item.Key.Contains('.'))
                        {
                            if (clone == null)
                                return obj.ReplaceDotInKeys();
                            ((IDictionary<string, object>)clone)[item.Key.Replace('.', '_')] = item.Value;
                        }
                        else if (clone != null)
                        {
                            ((IDictionary<string, object>)clone)[item.Key] = item.Value;
                        }
                        break;
                }
            }
            return clone ?? obj;
        }
    }
}
