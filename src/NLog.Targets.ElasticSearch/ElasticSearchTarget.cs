﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Threading;
using Elasticsearch.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;

namespace NLog.Targets.ElasticSearch
{
    /// <summary>
    /// NLog Target for writing to ElasticSearch using low level client
    /// </summary>
    [Target("ElasticSearch")]
    public class ElasticSearchTarget : TargetWithLayout, IElasticSearchTarget
    {
        private IElasticLowLevelClient _client;
        private Layout _uri = "http://localhost:9200";
        private Layout _cloudId;
        private Layout _username;
        private Layout _password;
        private HashSet<string> _excludedProperties = new HashSet<string>(new[] { "CallerMemberName", "CallerFilePath", "CallerLineNumber", "MachineName", "ThreadId" });
        private JsonSerializer _jsonSerializer;
        private JsonSerializer _flatJsonSerializer;
        private readonly Lazy<JsonSerializerSettings> _jsonSerializerSettings = new Lazy<JsonSerializerSettings>(() => CreateJsonSerializerSettings(false), LazyThreadSafetyMode.PublicationOnly);
        private readonly Lazy<JsonSerializerSettings> _flatSerializerSettings = new Lazy<JsonSerializerSettings>(() => CreateJsonSerializerSettings(true), LazyThreadSafetyMode.PublicationOnly);

        private JsonSerializer JsonSerializer => _jsonSerializer ?? (_jsonSerializer = JsonSerializer.CreateDefault(_jsonSerializerSettings.Value));
        private JsonSerializer JsonSerializerFlat => _flatJsonSerializer ?? (_flatJsonSerializer = JsonSerializer.CreateDefault(_flatSerializerSettings.Value));

        /// <summary>
        /// Gets or sets a connection string name to retrieve the Uri from.
        ///
        /// Use as an alternative to Uri
        /// </summary>
        [Obsolete("Deprecated. Please use the configsetting layout renderer instead.", true)]
        public string ConnectionStringName { get; set; }

        /// <summary>
        /// Gets or sets the elasticsearch uri, can be multiple comma separated.
        /// </summary>
        public string Uri
        {
            get => (_uri as SimpleLayout)?.Text;
            set
            {
                _uri = value ?? string.Empty;

                if (IsInitialized)
                {
                    InitializeTarget();
                }
            }
        }

        /// <summary>
        /// Gets or sets the elasticsearch cloud id.
        /// </summary>
        public string CloudId
        {
            get => (_cloudId as SimpleLayout)?.Text;
            set
            {
                _cloudId = value ?? string.Empty;

                if (IsInitialized)
                {
                    InitializeTarget();
                }
            }
        }

        /// <summary>
        /// Set it to true if ElasticSearch uses BasicAuth
        /// </summary>
        public bool RequireAuth { get; set; }

        /// <summary>
        /// Username for basic auth
        /// </summary>
        public string Username { get => (_username as SimpleLayout)?.Text; set => _username = value ?? string.Empty; }

        /// <summary>
        /// Password for basic auth
        /// </summary>
        public string Password { get => (_password as SimpleLayout)?.Text; set => _password = value ?? string.Empty; }

        /// <inheritdoc />
        public WebProxy Proxy { get; set; }

        /// <summary>
        /// Gets or sets the proxy address
        /// </summary>
        public Layout ProxyAddress { get; set; }

        /// <summary>
        /// Gets or sets the proxy username
        /// </summary>
        public Layout ProxyUserName { get; set; }

        /// <summary>
        /// Gets or sets the proxy password
        /// </summary>
        public Layout ProxyPassword { get; set; }

        /// <summary>
        /// Set it to true to disable proxy detection
        /// </summary>
        public bool DisableAutomaticProxyDetection { get; set; }

        /// <summary>
        /// Set it to true to disable SSL certificate validation
        /// </summary>
        public bool DisableCertificateValidation { get; set; }

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
        [Obsolete("No longer needed", true)]
        public bool ThrowExceptions { get; set; }

        /// <summary>
        /// Gets or sets whether it should perform safe object-reflection (-1 = Unsafe, 0 - No Reflection, 1 - Simple Reflection, 2 - Full Reflection)
        /// </summary>
        public int MaxRecursionLimit { get; set; } = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="ElasticSearchTarget"/> class.
        /// </summary>
        public ElasticSearchTarget()
        {
            Name = "ElasticSearch";
            OptimizeBufferReuse = true;
        }

        /// <inheritdoc />
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            IConnectionPool connectionPool;

            var eventInfo = LogEventInfo.CreateNullEvent();
            var cloudId = _cloudId?.Render(eventInfo) ?? string.Empty;
            if (!String.IsNullOrWhiteSpace(cloudId))
            {
                var username = _username?.Render(eventInfo) ?? string.Empty;
                var password = _password?.Render(eventInfo) ?? string.Empty;
                connectionPool = new CloudConnectionPool(
                    cloudId,
                    new BasicAuthenticationCredentials(
                        username,
                        password));
            }
            else
            {
                var uri = _uri?.Render(eventInfo) ?? string.Empty;
                var nodes = uri.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(url => new Uri(url));
                connectionPool = new StaticConnectionPool(nodes);  
            }

            var config = ElasticsearchSerializer == null
                ? new ConnectionConfiguration(connectionPool)
                : new ConnectionConfiguration(connectionPool, ElasticsearchSerializer);

            if (RequireAuth)
            {
                var username = _username?.Render(eventInfo) ?? string.Empty;
                var password = _password?.Render(eventInfo) ?? string.Empty;
                config = config.BasicAuthentication(username, password);
            }

            if (DisableAutomaticProxyDetection)
                config = config.DisableAutomaticProxyDetection();

            if (DisableCertificateValidation)
                config = config.ServerCertificateValidationCallback((o, certificate, chain, errors) => true).ServerCertificateValidationCallback(CertificateValidations.AllowAll);

            if (DisablePing)
                config = config.DisablePing();

            if (Proxy != null)
            {
                if (Proxy.Credentials == null)
                {
                    throw new InvalidOperationException("Proxy credentials should be specified.");
                }

                if (!(Proxy.Credentials is NetworkCredential))
                {
                    throw new InvalidOperationException($"Type {Proxy.Credentials.GetType().FullName} of proxy credentials isn't supported. Use {typeof(NetworkCredential).FullName} instead.");
                }

                var credential = (NetworkCredential)Proxy.Credentials;
                config = config.Proxy(Proxy.Address, credential.UserName, credential.SecurePassword);
            }
            else if (ProxyAddress != null)
            {
                var proxyAddress = ProxyAddress.Render(eventInfo);
                var proxyUserName = ProxyUserName?.Render(eventInfo) ?? string.Empty;
                var proxyPassword = ProxyPassword?.Render(eventInfo) ?? string.Empty;
                if (!string.IsNullOrEmpty(proxyAddress))
                {
                    config = config.Proxy(new Uri(proxyAddress), proxyUserName, proxyPassword);
                }
            }

            if (EnableHttpCompression)
                config = config.EnableHttpCompression();

            _client = new ElasticLowLevelClient(config);

            if (!string.IsNullOrEmpty(ExcludedProperties))
                _excludedProperties = new HashSet<string>(ExcludedProperties.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <inheritdoc />
        protected override void Write(AsyncLogEventInfo logEvent)
        {
            SendBatch(new[] { logEvent });
        }

        /// <inheritdoc />
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

                if (logEvent.Exception != null && !document.ContainsKey("exception"))
                {
                    document.Add("exception", FormatValueSafe(logEvent.Exception, "exception"));
                }

                if (IncludeAllProperties && logEvent.HasProperties)
                {
                    foreach (var p in logEvent.Properties)
                    {
                        var propertyKey = p.Key.ToString();
                        if (_excludedProperties.Contains(propertyKey))
                            continue;

                        if (document.ContainsKey(propertyKey))
                        {
                            propertyKey += "_1";
                            if (document.ContainsKey(propertyKey))
                                continue;
                        }

                        document[propertyKey] = FormatValueSafe(p.Value, propertyKey);
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

        private object FormatValueSafe(object value, string propertyName)
        {
            try
            {
                if (value is Exception)
                {
                    if (MaxRecursionLimit == 0 || MaxRecursionLimit == 1)
                        return FormatToExpandoObject(value, JsonSerializerFlat);
                    else
                        return FormatToExpandoObject(value, JsonSerializer);
                }
                else if (MaxRecursionLimit >= 0)
                {
                    if (Convert.GetTypeCode(value) != TypeCode.Object || value.GetType().IsValueType)
                    {
                        return value;
                    }
                    else if (MaxRecursionLimit == 0)
                    {
                        if (value is System.Collections.IEnumerable)
                            return null;
                        else
                            return value.ToString();
                    }
                    else if (JsonSerializer.ContractResolver.ResolveContract(value.GetType()) is Newtonsoft.Json.Serialization.JsonObjectContract)
                    {
                        if (MaxRecursionLimit == 1)
                        {
                            return FormatToExpandoObject(value, JsonSerializerFlat);
                        }
                        else if (MaxRecursionLimit > 1)
                        {
                            return FormatToExpandoObject(value, JsonSerializer);
                        }
                    }
                }

                return value;
            }
            catch (Exception ex)
            {
                _jsonSerializer = null; // Reset as it might now be in bad state
                _flatJsonSerializer = null;
                InternalLogger.Error(ex, "ElasticSearch: Error while formatting property: {0}", propertyName);
                return null;
            }
        }

        private static object FormatToExpandoObject(object value, JsonSerializer jsonSerializer)
        {
            var sb = new System.Text.StringBuilder(256);
            var sw = new System.IO.StringWriter(sb, System.Globalization.CultureInfo.InvariantCulture);
            using (JsonTextWriter jsonWriter = new JsonTextWriter(sw))
            {
                jsonWriter.Formatting = jsonSerializer.Formatting;
                jsonSerializer.Serialize(jsonWriter, value, value.GetType());
            }
            var expandoObject = sb.ToString().ToSystemType(typeof(object), null, jsonSerializer);
            if (value is Exception && expandoObject is IDictionary<string, object> dictionary)
            {
                dictionary["Type"] = value.GetType().ToString();
            }
            return expandoObject;
        }

        private static JsonSerializerSettings CreateJsonSerializerSettings(bool specialPropertyResolver)
        {
            var jsonSerializerSettings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, NullValueHandling = NullValueHandling.Ignore, CheckAdditionalContent = true };
            jsonSerializerSettings.Converters.Add(new StringEnumConverter());
            jsonSerializerSettings.Error = (sender, args) =>
            {
                InternalLogger.Warn(args.ErrorContext.Error, $"Error serializing exception property '{args.ErrorContext.Member}', property ignored");
                args.ErrorContext.Handled = true;
            };
            if (specialPropertyResolver)
            {
                jsonSerializerSettings.ContractResolver = new FlatObjectContractResolver();
            }
            return jsonSerializerSettings;
        }

        /// <summary>
        /// Serializes all non-simple properties as object.ToString()
        /// </summary>
        private sealed class FlatObjectContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
        {
            private readonly FlatObjectConverter _flatObjectConverter = new FlatObjectConverter();

            protected override Newtonsoft.Json.Serialization.JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
            {
                var jsonProperty = base.CreateProperty(member, memberSerialization);
                if (jsonProperty.Readable && !IsSimpleType(jsonProperty.PropertyType))
                    jsonProperty.Converter = _flatObjectConverter;
                return jsonProperty;
            }

            private static bool IsSimpleType(Type propertyType)
            {
                return propertyType != null && (Type.GetTypeCode(propertyType) != TypeCode.Object || propertyType.IsValueType);
            }

            private class FlatObjectConverter : JsonConverter
            {
                public override bool CanConvert(Type objectType)
                {
                    return true;
                }

                public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
                {
                    return null;
                }

                public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                {
                    if (value == null)
                    {
                        writer.WriteNull();
                    }
                    else if (value is System.Collections.IEnumerable)
                    {
                        writer.WriteNull();
                    }
                    else
                    {
                        writer.WriteValue(value.ToString());
                    }
                }
            }
        }
    }
}
