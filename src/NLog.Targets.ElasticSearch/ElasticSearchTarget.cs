using Elasticsearch.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;

namespace NLog.Targets.ElasticSearch
{
    [Target("ElasticSearch")]
    public class ElasticSearchTarget : TargetWithLayout, IElasticSearchTarget
    {
        private IElasticLowLevelClient _client;
        private Layout _uri = "http://localhost:9200";
        private HashSet<string> _excludedProperties = new HashSet<string>(new[] { "CallerMemberName", "CallerFilePath", "CallerLineNumber", "MachineName", "ThreadId" });
        private JsonSerializer _jsonSerializer;
        private readonly Lazy<JsonSerializerSettings> _jsonSerializerSettings = new Lazy<JsonSerializerSettings>(CreateJsonSerializerSettings, LazyThreadSafetyMode.PublicationOnly);

        private JsonSerializer JsonSerializer => _jsonSerializer ?? (_jsonSerializer = JsonSerializer.CreateDefault(_jsonSerializerSettings.Value));

        /// <summary>
        /// Gets or sets a connection string name to retrieve the Uri from.
        /// 
        /// Use as an alternative to Uri
        /// </summary>
        public string ConnectionStringName { get; set; }

        /// <summary>
        /// Gets or sets the elasticsearch uri, can be multiple comma separated.
        /// </summary>
        public string Uri { get => (_uri as SimpleLayout)?.Text; set => _uri = value ?? string.Empty; }

        /// <summary>
        /// Set it to true if ElasticSearch uses BasicAuth
        /// </summary>
        public bool RequireAuth { get; set; }

        /// <summary>
        /// Username for basic auth
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Password for basic auth
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Set it to true to disable proxy detection
        /// </summary>
        public bool DisableAutomaticProxyDetection { get; set; }

        /// <summary>
        /// Set it to true to disable use of ping to checking if node is alive
        /// </summary>
        public bool DisablePing { get; set; }

        /// <summary>
        /// Set it to true to enable HttpCompression (Must be enabled on server)
        /// </summary>
        public bool EnableHttpCompression { get; set; }

        /// <summary>
        /// Gets or sets the name of the elasticsearch index to write to.
        /// </summary>
        public Layout Index { get; set; } = "logstash-${date:format=yyyy.MM.dd}";

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
        public Layout DocumentType { get; set; } = "logevent";

        /// <summary>
        /// Gets or sets the pipeline transformation
        /// </summary>
        public Layout Pipeline { get; set; }

        /// <summary>
        /// Gets or sets a list of additional fields to add to the elasticsearch document.
        /// </summary>
        [ArrayParameter(typeof(Field), "field")]
        public IList<Field> Fields { get; set; } = new List<Field>();

        /// <summary>
        /// Gets or sets an alternative serializer for the elasticsearch client to use.
        /// </summary>
        public IElasticsearchSerializer ElasticsearchSerializer { get; set; }

        /// <summary>
        /// Gets or sets if exceptions will be rethrown.
        /// 
        /// Set it to true if ElasticSearchTarget target is used within FallbackGroup target (https://github.com/NLog/NLog/wiki/FallbackGroup-target).
        /// </summary>
        [Obsolete("No longer needed")]
        public bool ThrowExceptions { get; set; }

        public ElasticSearchTarget()
        {
            Name = "ElasticSearch";
            OptimizeBufferReuse = true;
        }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            var uri = ConnectionStringName.GetConnectionString() ?? (_uri?.Render(LogEventInfo.CreateNullEvent())) ?? string.Empty;
            var nodes = uri.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(url => new Uri(url));
            var connectionPool = new StaticConnectionPool(nodes);

            var config = new ConnectionConfiguration(connectionPool);

            if (ElasticsearchSerializer != null)
                config = new ConnectionConfiguration(connectionPool, ElasticsearchSerializer);

            if (RequireAuth)
                config.BasicAuthentication(Username, Password);

            if (DisableAutomaticProxyDetection)
                config.DisableAutomaticProxyDetection();

            if (DisablePing)
                config.DisablePing();

            if (EnableHttpCompression)
                config.EnableHttpCompression();

            _client = new ElasticLowLevelClient(config);

            if (!string.IsNullOrEmpty(ExcludedProperties))
                _excludedProperties = new HashSet<string>(ExcludedProperties.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }

        protected override void Write(AsyncLogEventInfo logEvent)
        {
            SendBatch(new[] { logEvent });
        }

        protected override void Write(IList<AsyncLogEventInfo> logEvents)
        {
            SendBatch(logEvents);
        }

        private void SendBatch(ICollection<AsyncLogEventInfo> logEvents)
        {
            try
            {
                var payload = FormPayload(logEvents);

                var result = _client.Bulk<BytesResponse>(payload);

                var exception = result.Success ? null : result.OriginalException ?? new Exception("No error message. Enable Trace logging for more information.");
                if (exception != null)
                {
                    InternalLogger.Error(exception.FlattenToActualException(), $"ElasticSearch: Failed to send log messages. status={result.HttpStatusCode}");
                }

                foreach (var ev in logEvents)
                {
                    ev.Continuation(exception);
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex.FlattenToActualException(), "ElasticSearch: Error while sending log messages");
                foreach (var ev in logEvents)
                {
                    ev.Continuation(ex);
                }
            }
        }

        private PostData FormPayload(ICollection<AsyncLogEventInfo> logEvents)
        {
            var payload = new List<object>(logEvents.Count * 2);    // documentInfo + document

            foreach (var ev in logEvents)
            {
                var logEvent = ev.LogEvent;

                var document = new Dictionary<string, object>
                {
                    {"@timestamp", logEvent.TimeStamp},
                    {"level", logEvent.Level.Name},
                    {"message", RenderLogEvent(Layout, logEvent)}
                };

                if (logEvent.Exception != null)
                {
                    var jsonString = JsonConvert.SerializeObject(logEvent.Exception, _jsonSerializerSettings.Value);
                    var ex = JsonConvert.DeserializeObject<ExpandoObject>(jsonString);
                    document.Add("exception", ex.ReplaceDotInKeys());
                }

                foreach (var field in Fields)
                {
                    var renderedField = RenderLogEvent(field.Layout, logEvent);

                    if (string.IsNullOrWhiteSpace(renderedField))
                        continue;

                    try
                    {
                        document[field.Name] = renderedField.ToSystemType(field.LayoutType, logEvent.FormatProvider, JsonSerializer);
                    }
                    catch (Exception ex)
                    {
                        _jsonSerializer = null; // Reset as it might now be in bad state
                        InternalLogger.Error(ex, "ElasticSearch: Error while formatting field: {0}", field.Name);
                    }
                }

                if (IncludeAllProperties && logEvent.HasProperties)
                {
                    foreach (var p in logEvent.Properties)
                    {
                        var propertyKey = p.Key.ToString();
                        if (_excludedProperties.Contains(propertyKey))
                            continue;
                        if (document.ContainsKey(propertyKey))
                            continue;

                        document[propertyKey] = p.Value;
                    }
                }

                var index = RenderLogEvent(Index, logEvent).ToLowerInvariant();
                var type = RenderLogEvent(DocumentType, logEvent);

                object documentInfo;
                if (Pipeline == null)
                    documentInfo = new { index = new { _index = index, _type = type } };
                else
                {
                    var pipeLine = RenderLogEvent(Pipeline, logEvent);
                    documentInfo = new { index = new { _index = index, _type = type, pipeline = pipeLine } };
                }

                payload.Add(documentInfo);
                payload.Add(document);
            }

            return PostData.MultiJson(payload);
        }

        private static JsonSerializerSettings CreateJsonSerializerSettings()
        {
            var jsonSerializerSettings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, CheckAdditionalContent = true };
            jsonSerializerSettings.Converters.Add(new StringEnumConverter());
            return jsonSerializerSettings;
        }
    }
}
