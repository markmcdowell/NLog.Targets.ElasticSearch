using System;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;
using Xunit;
using Xunit.Abstractions;

namespace NLog.Targets.ElasticSearch.Tests
{
    public class IntegrationTests
    {
        private readonly ITestOutputHelper testOutputHelper;

        public IntegrationTests(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        private class ExceptionWithPropertiesThatThrow : Exception
        {
            public object ThisPropertyThrowsOnGet => throw new ObjectDisposedException("DisposedObject");
        }

        [Theory(Skip = "Integration")]
        [InlineData(true)]
        [InlineData(false)]
        public void ExceptionSerializationTest(bool hasExceptionFieldLayout)
        {
            using (var testOutputTextWriter = new TestOutputTextWriter(testOutputHelper))
            {
                InternalLogger.LogWriter = testOutputTextWriter;
                InternalLogger.LogLevel = LogLevel.Warn;

                var elasticTarget = new ElasticSearchTarget();

                if (hasExceptionFieldLayout)
                {
                    elasticTarget.Fields.Add(new Field
                    {
                        Name = "exception",
                        Layout = Layout.FromString("${exception:format=toString,Data:maxInnerExceptionLevel=10}"),
                        LayoutType = typeof(string)
                    });
                }

                var rule = new LoggingRule("*", LogLevel.Info, elasticTarget);

                var config = new LoggingConfiguration();
                config.LoggingRules.Add(rule);

                LogManager.Configuration = config;

                var logger = LogManager.GetLogger("Example");

                logger.Error(new ExceptionWithPropertiesThatThrow(), "Boom");

                LogManager.Flush();
            }
        }

        [Fact(Skip = "Integration")]
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