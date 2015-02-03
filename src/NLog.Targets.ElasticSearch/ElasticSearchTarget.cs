using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Elasticsearch.Net.Connection;
using Elasticsearch.Net.ConnectionPool;
using Elasticsearch.Net.Serialization;
using Newtonsoft.Json;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;

namespace NLog.Targets.ElasticSearch
{
    [Target("ElasticSearch")]
    public class ElasticSearchTarget : TargetWithLayout
    {
        private IElasticsearchClient _client;

        public string ConnectionName { get; set; }

        public string Host { get; set; }

        [DefaultValue(9200)]
        public int Port { get; set; }

        public Layout Index { get; set; }

        [RequiredParameter]
        public Layout DocumentType { get; set; }

        [ArrayParameter(typeof(ElasticSearchField), "field")]
        public IList<ElasticSearchField> Fields { get; private set; }

        public ElasticSearchTarget()
        {
            Port = 9200;
            Host = "localhost";
            DocumentType = "logevent";
            Index = "logstash-${date:format=yyyy.MM.dd}";
            Fields = new List<ElasticSearchField>();
        }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            ConnectionStringSettings connectionStringSettings = null;
            if (!string.IsNullOrEmpty(ConnectionName))
                connectionStringSettings = ConfigurationManager.ConnectionStrings[ConnectionName];

            var nodes = connectionStringSettings != null ? connectionStringSettings.ConnectionString.Split(',').Select(url => new Uri(url)) : new[] { new Uri(string.Format("http://{0}:{1}", Host, Port)) };
            var connectionPool = new StaticConnectionPool(nodes);
            var config = new ConnectionConfiguration(connectionPool);
            _client = new ElasticsearchClient(config, serializer: new NewtonsoftJsonSerializer());
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
                        document[field.Name] = renderedField;
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

        public class NewtonsoftJsonSerializer : IElasticsearchSerializer
        {
            public T Deserialize<T>(Stream stream)
            {
                if (stream == null)
                    return default(T);

                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    byte[] buffer = ms.ToArray();
                    if (buffer.Length <= 1)
                        return default(T);
                    return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(buffer));
                }
            }

            public System.Threading.Tasks.Task<T> DeserializeAsync<T>(System.IO.Stream stream)
            {
                return new Task<T>(() => Deserialize<T>(stream));
            }

            const int BUFFER_SIZE = 1024;


            public byte[] Serialize(object data, SerializationFormatting formatting = SerializationFormatting.Indented)
            {
                return
                    Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data,
                        formatting == SerializationFormatting.Indented ? Formatting.Indented : Formatting.None));
            }

            public string Stringify(object valueType)
            {
                return ElasticsearchDefaultSerializer.DefaultStringify(valueType);
            }

            public static string DefaultStringify(object valueType)
            {
                var s = valueType as string;
                if (s != null)
                    return s;
                var ss = valueType as string[];
                if (ss != null)
                    return string.Join(",", ss);

                var pns = valueType as IEnumerable<object>;
                if (pns != null)
                    return string.Join(",", pns);

                var e = valueType as Enum;
                if (e != null) return KnownEnums.Resolve(e);
                if (valueType is bool)
                    return ((bool) valueType) ? "true" : "false";
                return valueType.ToString();
            }
        }
    }
}
