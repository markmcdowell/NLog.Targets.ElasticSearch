using NLog.Config;
using NUnit.Framework;

namespace NLog.Targets.ElasticSearch.Tests
{
    [TestFixture]
    public class ElasticSearchTargetTests
    {
        [Test]
        public void OutputTest()
        {
            var config = new LoggingConfiguration();

            var elasticSearchTarget = new ElasticSearchTarget();
            config.AddTarget("file", elasticSearchTarget);

            elasticSearchTarget.Layout = @"${logger} | ${windows-identity:userName=True:domain=False} | ${threadid} | ${message}";

            var rule = new LoggingRule("*", LogLevel.Debug, elasticSearchTarget);
            config.LoggingRules.Add(rule);

            LogManager.Configuration = config;

            var logger = LogManager.GetLogger("Example");
            logger.Trace("trace log message");
            logger.Debug("debug log message: {0}", 1);
            logger.Info("info log message");
            logger.Warn("warn log message");
            logger.Error("error log message");
            logger.Fatal("fatal log message");
        }
    }
}