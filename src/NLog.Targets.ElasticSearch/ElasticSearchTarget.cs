using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using Elasticsearch.Net;
using Elasticsearch.Net.Connection;
using Elasticsearch.Net.ConnectionPool;
using Elasticsearch.Net.Serialization;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;

namespace NLog.Targets.ElasticSearch
{
    [Target("ElasticSearch")]
    public class ElasticSearchTarget : TargetWithLayout
    {
        private IElasticsearchClient _client;
        private List<string> _excludedProperties = new List<string>(new[] { "CallerMemberName", "CallerFilePath", "CallerLineNumber", "MachineName", "ThreadId" }); 

        public string ConnectionStringName { get; set; }

        public string Uri { get; set; }

        public Layout Index { get; set; }

        public bool IncludeAllProperties { get; set; }
        public string ExcludedProperties { get; set; }

        public Type ConnectionConfigurationFactory { get; set; }

        [RequiredParameter]
        public Layout DocumentType { get; set; }

        [ArrayParameter(typeof(ElasticSearchField), "field")]
        public IList<ElasticSearchField> Fields { get; private set; }

        public IElasticsearchSerializer ElasticsearchSerializer { get; set; }

        public ElasticSearchTarget()
        {
            Uri = "http://localhost:9200";
            DocumentType = "logevent";
            Index = "logstash-${date:format=yyyy.MM.dd}";
            Fields = new List<ElasticSearchField>();
        }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            var uri = GetConnectionString(ConnectionStringName) ?? Uri;
            var nodes = uri.Split(',').Select(url => new Uri(url));
            var connectionPool = new StaticConnectionPool(nodes);

            IElasticSearchConnectionConfigurationFactory elasticSearchConnectionConfigurationFactory = null;

            if (ConnectionConfigurationFactory != null)
            {
                elasticSearchConnectionConfigurationFactory =
                    ConnectionConfigurationFactory.AsElasticSearchConnectionConfigurationFactory();
            }

            var config = elasticSearchConnectionConfigurationFactory == null
                ? new ConnectionConfiguration(connectionPool)
                : elasticSearchConnectionConfigurationFactory.Create(connectionPool);

            _client = new ElasticsearchClient(config, serializer:ElasticsearchSerializer);

            if (!String.IsNullOrEmpty(ExcludedProperties))
                _excludedProperties = new List<string>(ExcludedProperties.Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private string GetConnectionString(string name)
        {
            string value = GetEnvironmentVariable(name);
            if (!String.IsNullOrEmpty(value))
                return value;

            var connectionString = ConfigurationManager.ConnectionStrings[name];
            return connectionString != null ? connectionString.ConnectionString : null;
        }

        private string GetEnvironmentVariable(string name) {
            if (String.IsNullOrEmpty(name))
                return null;

            return Environment.GetEnvironmentVariable(name);
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
            var logEvents = events.Select(e => e.LogEvent);
            var payload = new List<object>();

            foreach (var logEvent in logEvents)
            {
                var document = new Dictionary<string, object>();
                document.Add("@timestamp", logEvent.TimeStamp);
                document.Add("level", logEvent.Level.Name);
                if (logEvent.Exception != null)
                    document.Add("exception", logEvent.Exception.ToString());
                document.Add("message", Layout.Render(logEvent));
                foreach (var field in Fields)
                {
                    var renderedField = field.Layout.Render(logEvent);
                    if (!string.IsNullOrWhiteSpace(renderedField))
                        document[field.Name] = renderedField.ToSystemType(field.LayoutType);
                }

                if (IncludeAllProperties)
                {
                    foreach (var p in logEvent.Properties.Where(p => !_excludedProperties.Contains(p.Key)))
                    {
                        if (document.ContainsKey(p.Key.ToString()))
                            continue;

                        document[p.Key.ToString()] = p.Value;
                    }
                }

                var index = Index.Render(logEvent).ToLowerInvariant();
                var type = DocumentType.Render(logEvent);

                payload.Add(new { index = new { _index = index, _type = type } });
                payload.Add(document);
            }

            try
            {
                var result = _client.Bulk<byte[]>(payload);
                if (!result.Success)
                    InternalLogger.Error("Failed to send log messages to ElasticSearch: status={0} message=\"{1}\"", result.HttpStatusCode, result.OriginalException.Message);
            }
            catch (Exception ex)
            {
                InternalLogger.Error("Error while sending log messages to ElasticSearch: message=\"{0}\"", ex.Message);
            }
        }
    }
}
