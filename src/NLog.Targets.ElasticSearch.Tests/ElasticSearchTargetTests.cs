using System;
using System.Threading;
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
            GlobalDiagnosticsContext.Set("test", null);
            logger.Trace("trace log message");
            logger.Debug("debug log message: {0}", 1);
            logger.Info("info log message");
            GlobalDiagnosticsContext.Set("test", "hi");
            logger.Warn("warn log message");
            logger.Error("error log message");
            logger.Fatal("fatal log message");
            var ev = new LogEventInfo();
            ev.TimeStamp = DateTime.Now;
            ev.Level = LogLevel.Error;
            ev.Message = "log with property";
            ev.Properties["hello"] = new TestData { Val1 = "hello" };
            logger.Log(ev);
            Thread.Sleep(500);
        }

        public class TestData {
            public string Val1 { get; set; }
        }
    }
}