using System.IO;
using System.Text;
using Xunit.Abstractions;

namespace NLog.Targets.ElasticSearch.Tests
{
    public class TestOutputTextWriter : TextWriter
    {
        private StringBuilder stringBuilder;
        private readonly ITestOutputHelper testOutputHelper;

        public TestOutputTextWriter(ITestOutputHelper testOutputHelper)
        {
            this.stringBuilder = new StringBuilder();
            this.testOutputHelper = testOutputHelper;
        }

        public override Encoding Encoding => Encoding.Unicode;

        public override void Write(char value)
        {
            this.stringBuilder.Append(value);
        }

        public override void WriteLine(string value)
        {
            this.stringBuilder.Append(value);

            this.Flush();
        }

        public override void Flush()
        {
            var sb = this.stringBuilder;
            if (sb.Length > 0)
            {
                this.stringBuilder = new StringBuilder();
                this.testOutputHelper.WriteLine(sb.ToString());
            }
        }

        protected override void Dispose(bool disposing)
        {
            this.Flush();
            base.Dispose(disposing);
        }
    }
}