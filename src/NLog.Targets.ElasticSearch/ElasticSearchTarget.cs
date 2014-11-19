using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Elasticsearch.Net;
using Elasticsearch.Net.Connection;
using Elasticsearch.Net.ConnectionPool;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;

namespace NLog.Targets.ElasticSearch
{
    [Target("ElasticSearch")]
    public class ElasticSearchTarget : TargetWithLayout
    {
        private IElasticsearchClient _client;

        [RequiredParameter]
        public string Host { get; set; }

        [DefaultValue(25)]
        public int Port { get; set; }

        [RequiredParameter]
        public Layout Index { get; set; }

        [RequiredParameter]
        public Layout Type { get; set; }

        public ElasticSearchTarget()
        {
            Port = 9200;
            Host = "localhost";                        
        }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            var node = new Uri(string.Format("http://{0}:{1}", Host, Port));
            var connectionPool = new SniffingConnectionPool(new[] { node });
            var config = new ConnectionConfiguration(connectionPool);
            _client = new ElasticsearchClient(config);
        }

        protected override void Write(AsyncLogEventInfo logEvent)
        {
            Write(new []{logEvent});
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
                    document.Add("exception", logEvent.Exception);
                document.Add("message", Layout.Render(logEvent));
                document.Add("fields", logEvent.Properties);

                payload.Add(new { index = new { _index = Index.Render(logEvent), _type = Type.Render(logEvent) } });
                payload.Add(document);
            }

            _client.Bulk(payload);
        }
    }
}