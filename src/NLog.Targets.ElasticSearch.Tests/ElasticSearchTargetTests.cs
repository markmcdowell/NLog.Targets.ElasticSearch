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
            var config = new XmlLoggingConfiguration("NLog.Targets.ElasticSearch.Tests.dll.config");

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