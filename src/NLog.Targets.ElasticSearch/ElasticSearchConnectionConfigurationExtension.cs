using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NLog.Targets.ElasticSearch
{
    public static class ElasticSearchConnectionConfigurationExtension
    {
        public static IElasticSearchConnectionConfigurationFactory AsConnectionConfigurationFActory(this Type type)
        {
            var instance = Activator.CreateInstance(type);

            return instance is IElasticSearchConnectionConfigurationFactory ? instance as IElasticSearchConnectionConfigurationFactory : null;
        }
    }
}
