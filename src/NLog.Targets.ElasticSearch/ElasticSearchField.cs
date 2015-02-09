using System;
using NLog.Config;
using NLog.Layouts;

namespace NLog.Targets.ElasticSearch
{
    [NLogConfigurationItem]
    public class ElasticSearchField
    {
        public ElasticSearchField()
        {
            LayoutType = typeof (string);
        }

        [RequiredParameter]
        public string Name { get; set; }

        [RequiredParameter]
        public Layout Layout { get; set; }

        public Type LayoutType { get; set; }

        public override string ToString()
        {
            return string.Format("Name: {0}, LayoutType: {1}, Layout: {2}", Name, LayoutType, Layout);
        }
    }
}