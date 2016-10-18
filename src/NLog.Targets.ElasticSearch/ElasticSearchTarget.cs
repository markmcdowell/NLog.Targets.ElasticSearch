using System;
using System.Collections.Generic;
using System.Linq;
using Elasticsearch.Net;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;
using Newtonsoft.Json;
using System.Dynamic;

namespace NLog.Targets.ElasticSearch
{
    [Target("ElasticSearch")]
    public class ElasticSearchTarget : TargetWithLayout, IElasticSearchTarget
    {
        private IElasticLowLevelClient _client;
        private List<string> _excludedProperties = new List<string>(new[] { "CallerMemberName", "CallerFilePath", "CallerLineNumber", "MachineName", "ThreadId" });

        /// <summary>
        /// Gets or sets a connection string name to retrieve the Uri from.
        /// 
        /// Use as an alternative to Uri
        /// </summary>
        public string ConnectionStringName { get; set; }

        /// <summary>
        /// Gets or sets the elasticsearch uri, can be multiple comma separated.
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        /// Gets or sets the name of the elasticsearch index to write to.
        /// </summary>
        public Layout Index { get; set; }

        /// <summary>
        /// Gets or sets whether to include all properties of the log event in the document
        /// </summary>
        public bool IncludeAllProperties { get; set; }

        /// <summary>
        /// Gets or sets a comma separated list of excluded properties when setting <see cref="IElasticSearchTarget.IncludeAllProperties"/>
        /// </summary>
        public string ExcludedProperties { get; set; }

        /// <summary>
        /// Gets or sets the document type for the elasticsearch index.
        /// </summary>
        [RequiredParameter]
        public Layout DocumentType { get; set; }

        /// <summary>
        /// Gets or sets a list of additional fields to add to the elasticsearch document.
        /// </summary>
        [ArrayParameter(typeof(Field), "field")]
        public IList<Field> Fields { get; set; }

        /// <summary>
        /// Gets or sets an alertnative serializer for the elasticsearch client to use.
        /// </summary>
        public IElasticsearchSerializer ElasticsearchSerializer { get; set; }

        /// <summary>
        /// Gets or sets if exceptions will be rethrown.
        /// 
        /// Set it to true if ElasticSearchTarget target is used within FallbackGroup target (https://github.com/NLog/NLog/wiki/FallbackGroup-target).
        /// </summary>
        public bool ThrowExceptions { get; set; }

        public ElasticSearchTarget()
        {
            Name = "ElasticSearch";
            Uri = "http://localhost:9200";
            DocumentType = "logevent";
            Index = "logstash-${date:format=yyyy.MM.dd}";
            Fields = new List<Field>();
        }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            var uri = ConnectionStringName.GetConnectionString() ?? Uri;
            var nodes = uri.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(url => new Uri(url));
            var connectionPool = new StaticConnectionPool(nodes);

            IConnectionConfigurationValues config = new ConnectionConfiguration(connectionPool);
            if (ElasticsearchSerializer != null)
                config = new ConnectionConfiguration(connectionPool, _ => ElasticsearchSerializer);

            _client = new ElasticLowLevelClient(config);

            if (!string.IsNullOrEmpty(ExcludedProperties))
                _excludedProperties = ExcludedProperties.Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        protected override void Write(AsyncLogEventInfo logEvent)
        {
            Write(new[] { logEvent });
        }

        protected override void Write(AsyncLogEventInfo[] logEvents)
        {
            SendBatch(logEvents);
        }

        private void SendBatch(IEnumerable<AsyncLogEventInfo> events)
        {
            try
            {
                var logEvents = events.Select(e => e.LogEvent);

                var payload = FormPayload(logEvents);

                var result = _client.Bulk<byte[]>(payload);

                if (result.Success)
                    return;

                InternalLogger.Error("Failed to send log messages to elasticsearch: status={0}, message=\"{1}\"",
                    result.HttpStatusCode, result.OriginalException?.Message ?? "No error message. Enable Trace logging for more information.");
                InternalLogger.Trace("Failed to send log messages to elasticsearch: result={0}", result);

                if (result.OriginalException != null)
                    throw result.OriginalException;
            }
            catch (Exception ex)
            {
                InternalLogger.Error("Error while sending log messages to elasticsearch: message=\"{0}\"", ex.Message);
                
                if (ThrowExceptions)
                    throw;
            }
        }

        private object FormPayload(IEnumerable<LogEventInfo> logEvents)
        {
            var payload = new List<object>();

            foreach (var logEvent in logEvents)
            {
                var document = new Dictionary<string, object>
                {
                    {"@timestamp", logEvent.TimeStamp},
                    {"level", logEvent.Level.Name},
                    {"message", Layout.Render(logEvent)}
                };

                if (logEvent.Exception != null)
                {
                    var jsonString = JsonConvert.SerializeObject(logEvent.Exception);

                    var ex = JsonConvert.DeserializeObject<ExpandoObject>(jsonString);

                    document.Add("exception", ex.ReplaceDotInKeys());
                }

                foreach (var field in Fields)
                {
                    var renderedField = field.Layout.Render(logEvent);
                    if (!string.IsNullOrWhiteSpace(renderedField))
                        document[field.Name] = renderedField.ToSystemType(field.LayoutType);
                }

                if (IncludeAllProperties)
                {
                    foreach (var p in logEvent.Properties.Where(p => !_excludedProperties.Contains(p.Key.ToString()))
                                                         .Where(p => !document.ContainsKey(p.Key.ToString())))
                    {
                        document[p.Key.ToString()] = p.Value;
                    }
                }

                var index = Index.Render(logEvent).ToLowerInvariant();
                var type = DocumentType.Render(logEvent);

                payload.Add(new { index = new { _index = index, _type = type } });
                payload.Add(document);
            }

            return payload;
        }
    }
}
