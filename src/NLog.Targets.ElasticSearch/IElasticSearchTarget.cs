using System;
using System.Collections.Generic;
using System.Net;
using Elasticsearch.Net;
using NLog.Layouts;

namespace NLog.Targets.ElasticSearch
{
    /// <summary>
    /// Interface for NLog Target for writing to ElasticSearch
    /// </summary>
    public interface IElasticSearchTarget
    {
        /// <summary>
        /// Gets or sets a connection string name to retrieve the Uri from.
        /// 
        /// Use as an alternative to Uri
        /// </summary>
        string ConnectionStringName { get; set; }

        /// <summary>
        /// Gets or sets the elasticsearch uri, can be multiple comma separated.
        /// </summary>
        string Uri { get; set; }

        /// <summary>
        /// Gets or sets the elasticsearch cloud id.
        /// <para>If both apiKey and apiKeyId is provided, <see cref="ApiKeyAuthenticationCredentials"/> will be used.</para>
        /// <para>Otherwise, <see cref="BasicAuthenticationCredentials"/> will be used.</para>
        /// </summary>
        string CloudId { get; set; }

        /// <summary>
        /// Set it to true if ElasticSearch uses BasicAuth
        /// </summary>
        [Obsolete]
        bool RequireAuth { get; set; }

        /// <summary>
        /// Username for basic auth
        /// </summary>
        string Username { get; set; }

        /// <summary>
        /// Password for basic auth
        /// </summary>
        string Password { get; set; }

        /// <summary>
        /// Gets or sets the proxy-destination
        /// </summary>
        WebProxy Proxy { get; set; }

        /// <summary>
        /// Gets or sets the proxy address
        /// </summary>
        Layout ProxyAddress { get; set; }

        /// <summary>
        /// Gets or sets the proxy username
        /// </summary>
        Layout ProxyUserName { get; set; }

        /// <summary>
        /// Gets or sets the proxy password
        /// </summary>
        Layout ProxyPassword { get; set; }

        /// <summary>
        /// Set it to true to disable proxy detection
        /// </summary>
        bool DisableAutomaticProxyDetection { get; set; }

        /// <summary>
        /// Set it to true to disable SSL certificate validation
        /// </summary>
        bool DisableCertificateValidation { get; set; }

        /// <summary>
        /// Set it to true to disable use of ping to checking if node is alive
        /// </summary>
        bool DisablePing { get; set; }

        /// <summary>
        /// Set it to true to enable HttpCompression (Must be enabled on server)
        /// </summary>
        bool EnableHttpCompression { get; set; }

        /// <summary>
        /// Gets or sets the name of the elasticsearch index to write to.
        /// </summary>
        Layout Index { get; set; }

        /// <summary>
        /// Gets or sets whether to include all properties of the log event in the document
        /// </summary>
        bool IncludeAllProperties { get; set; }

        /// <summary>
        /// Gets or sets a comma separated list of excluded properties when setting <see cref="IncludeAllProperties"/>
        /// </summary>
        string ExcludedProperties { get; set; }

        /// <summary>
        /// Gets or sets the document type for the elasticsearch index.
        /// </summary>
        Layout DocumentType { get; set; }

        /// <summary>
        /// Gets or sets a list of additional fields to add to the elasticsearch document.
        /// </summary>
        IList<Field> Fields { get; set; }

        /// <summary>
        /// Gets or sets an alertnative serializer for the elasticsearch client to use.
        /// </summary>
        IElasticsearchSerializer ElasticsearchSerializer { get; set; }

        /// <summary>
        /// Gets or sets the elasticsearch ApiKeyId.
        /// <para>Use with <see cref="ApiKey"/> and <see cref="CloudId"/>.</para>
        /// </summary>
        string ApiKeyId { get; set; }

        /// <summary>
        /// Gets or sets the elasticsearch ApiKey.
        /// <para>Use with <see cref="ApiKeyId"/> and <see cref="CloudId"/>.</para>
        /// </summary>
        string ApiKey { get; set; }

        /// <summary>
        /// <para>Automatically add @timestamp, level, message and exception.* properties.</para>
        /// <para>Set to false if you want to explicitly specify the document fields.</para>
        /// <para>Default value is true.</para>
        /// </summary>
        bool IncludeDefaultFields { get; set; }
    }
}