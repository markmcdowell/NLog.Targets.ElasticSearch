using System;
using NLog.Config;
using NUnit.Framework;

namespace NLog.Targets.ElasticSearch.Tests
{
    [TestFixture, Explicit]
    public class IntegrationTests
    {
        [Test]
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

        [Test]
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

        [Test]
        public void ReadFromConfigTest()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;

            LogManager.Configuration = new XmlLoggingConfiguration("NLog.Targets.ElasticSearch.Tests.dll.config");

            var logger = LogManager.GetLogger("Example");

            logger.Info("Hello elasticsearch");

            LogManager.Flush();
        }        
    }
}