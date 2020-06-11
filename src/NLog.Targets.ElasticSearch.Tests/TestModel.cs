using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;

namespace NLog.Targets.ElasticSearch.Tests
{
    public class TestModel
    {
        public ObjectId _id { get; set; }
        public string NoDuplicate { get; set; }
    }
}
