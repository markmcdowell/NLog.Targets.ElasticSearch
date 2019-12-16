using System;
using NLog.Config;
using NLog.Layouts;

namespace NLog.Targets.ElasticSearch
{
    /// <summary>
    /// Additional field details
    /// </summary>
    [NLogConfigurationItem]
    public class Field
    {
        /// <summary>
        /// Name of additional field
        /// </summary>
        [RequiredParameter]
        public string Name { get; set; }

        /// <summary>
        /// Value with NLog <see cref="NLog.Layouts.Layout"/> rendering support
        /// </summary>
        [RequiredParameter]
        public Layout Layout { get; set; } 

        /// <summary>
        /// Custom type conversion from default string to other type
        /// </summary>
        /// <remarks>
        /// <see cref="System.Object"/> can be used if the <see cref="Layout"/> renders JSON
        /// </remarks>
        public Type LayoutType { get; set; } = typeof(string);

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Name: {Name}, LayoutType: {LayoutType}, Layout: {Layout}";
        }
    }
}