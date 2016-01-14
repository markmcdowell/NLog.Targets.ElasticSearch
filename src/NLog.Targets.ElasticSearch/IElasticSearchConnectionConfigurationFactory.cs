using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Elasticsearch.Net.Connection;
using Elasticsearch.Net.ConnectionPool;

namespace NLog.Targets.ElasticSearch
{
    public interface IElasticSearchConnectionConfigurationFactory
    {
        ConnectionConfiguration Create( IConnectionPool connectionPool );
    }
}
