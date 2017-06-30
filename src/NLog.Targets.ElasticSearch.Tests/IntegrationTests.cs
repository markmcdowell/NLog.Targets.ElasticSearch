using System;
using NLog.Config;
using Xunit;

namespace NLog.Targets.ElasticSearch.Tests
{
    public class IntegrationTests
    {
        [Fact(Skip ="Integration")]
        public void SimpleLogTest()
        {
            var elasticTarget = new ElasticSearchTarget();

            var rule = new LoggingRule("*", elasticTarget);
            rule.EnableLoggingForLevel(LogLevel.Info);

            var config = new LoggingConfiguration();
            config.LoggingRules.Add(rule);

            LogManager.Configuration = config;

            var logger = LogManager.GetLogger("Example");

            logger.Info("Hello elasticsearch");

            LogManager.Flush();
        }

        [Fact(Skip = "Integration")]
        public void ExceptionTest()
        {
            var elasticTarget = new ElasticSearchTarget();

            var rule = new LoggingRule("*", elasticTarget);
            rule.EnableLoggingForLevel(LogLevel.Error);

            var config = new LoggingConfiguration();
            config.LoggingRules.Add(rule);

            LogManager.Configuration = config;

            var logger = LogManager.GetLogger("Example");

            var exception = new ArgumentException("Some random error message");

            logger.Error(exception, "An exception occured");

            LogManager.Flush();
        }

        [Fact(Skip = "Integration")]
        public void ReadFromConfigTest()
        {
            LogManager.Configuration = new XmlLoggingConfiguration("NLog.Targets.ElasticSearch.Tests.dll.config");

            var logger = LogManager.GetLogger("Example");

            logger.Info("Hello elasticsearch");

            LogManager.Flush();
        }
    }
}