using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
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

        public string ConnectionStringName { get; set; }

        public string Uri { get; set; }

        public Layout Index { get; set; }

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

            ConnectionStringSettings connectionStringSettings = null;
            if (!string.IsNullOrEmpty(ConnectionStringName))
                connectionStringSettings = ConfigurationManager.ConnectionStrings[ConnectionStringName];

            var uri = connectionStringSettings == null ? Uri : connectionStringSettings.ConnectionString;
            var nodes = uri.Split(',').Select(url => new Uri(url));
            var connectionPool = new StaticConnectionPool(nodes);
            var config = new ConnectionConfiguration(connectionPool);
            _client = new ElasticsearchClient(config, serializer:ElasticsearchSerializer);
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
