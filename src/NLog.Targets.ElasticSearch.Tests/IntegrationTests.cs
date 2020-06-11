using System;
using System.Net;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
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

                LogManager.ThrowExceptions = true;
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

            LogManager.ThrowExceptions = true;
            LogManager.Configuration = config;

            var logger = LogManager.GetLogger("Example");

            logger.Info("Hello elasticsearch");

            LogManager.Flush();
        }

        [Fact(Skip = "Integration")]
        public void SimpleJsonLayoutTest()
        {
            var elasticTarget = new ElasticSearchTarget();
            elasticTarget.EnableJsonLayout = true;
            elasticTarget.Layout = new JsonLayout()
            {
                MaxRecursionLimit = 10,
                IncludeAllProperties = true,
                Attributes =
                    {
                        new JsonAttribute("timestamp", "${date:universaltime=true:format=o}"),
                        new JsonAttribute("lvl", "${level}"),
                        new JsonAttribute("msg", "${message}"),
                        new JsonAttribute("logger", "${logger}"),
                        new JsonAttribute("threadid", "${threadid}", false), // Skip quotes for integer-value
                    }
            };

            var rule = new LoggingRule("*", elasticTarget);
            rule.EnableLoggingForLevel(LogLevel.Info);

            var config = new LoggingConfiguration();
            config.LoggingRules.Add(rule);

            LogManager.ThrowExceptions = true;
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

            LogManager.ThrowExceptions = true;
            LogManager.Configuration = config;

            var logger = LogManager.GetLogger("Example");

            var exception = new ArgumentException("Some random error message");

            logger.Error(exception, "An exception occured");

            LogManager.Flush();
        }

        [Fact(Skip = "Integration")]
        public void ReadFromConfigTest()
        {
            LogManager.ThrowExceptions = true;
            LogManager.Configuration = new XmlLoggingConfiguration("NLog.Targets.ElasticSearch.Tests.dll.config");

            var logger = LogManager.GetLogger("Example");

            logger.Info("Hello elasticsearch");

            LogManager.Flush();
        }

        [Fact(Skip = "Integration")]
        public void CustomJsonConverterExceptionTest()
        {
            var runner = MongoDbRunner.Start();

            try
            {
                var dbClient = new MongoClient(runner.ConnectionString);
                var database = dbClient.GetDatabase("Test");

                var collection = database.GetCollection<TestModel>("TestCollection");
                collection
                    .Indexes
                    .CreateOneAsync(
                        Builders<TestModel>.IndexKeys.Ascending(a => a.NoDuplicate),
                        new CreateIndexOptions { Unique = true });

                ElasticSearchTarget.AddJsonConverter(new JsonToStringConverter(typeof(IPAddress)));

                using (var testOutputTextWriter = new TestOutputTextWriter(testOutputHelper))
                {
                    InternalLogger.LogWriter = testOutputTextWriter;
                    InternalLogger.LogLevel = LogLevel.Error;

                    LogManager.Configuration = new XmlLoggingConfiguration("NLog.Targets.ElasticSearch.Tests.dll.config");

                    var logger = LogManager.GetLogger("Example");

                    var testModel1 = new TestModel
                    {
                        _id = ObjectId.GenerateNewId(),
                        NoDuplicate = "AAA"
                    };

                    collection.InsertOne(testModel1);

                    var exception = Assert.Throws<MongoCommandException>(() =>
                    {
                        var testModel2 = new TestModel
                        {
                            _id = ObjectId.GenerateNewId(),
                            NoDuplicate = "AAA"
                        };

                        collection.FindOneAndReplace(
                            Builders<TestModel>.Filter.Eq(a => a._id, ObjectId.GenerateNewId()),
                            testModel2,
                            new FindOneAndReplaceOptions<TestModel, TestModel>
                            {
                                ReturnDocument = ReturnDocument.Before,
                                IsUpsert = true
                            });
                    });

                    logger.Error(exception, "Failed to insert data");

                    LogManager.Flush();
                    Assert.False(testOutputTextWriter.HadErrors(), "Failed to log to elastic");
                }
            }
            finally
            {
                runner.Dispose();
            }
        }
    }
}