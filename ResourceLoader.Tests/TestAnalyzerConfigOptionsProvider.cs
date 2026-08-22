using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ResourceLoader.Tests
{
    internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly string _projectDir;

        public TestAnalyzerConfigOptionsProvider(string projectDir)
        {
            _projectDir = projectDir;
        }

        public override AnalyzerConfigOptions GlobalOptions => new TestAnalyzerConfigOptions(_projectDir);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestAnalyzerConfigOptions(_projectDir);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new TestAnalyzerConfigOptions(_projectDir);
    }

    internal sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly string _projectDir;

        public TestAnalyzerConfigOptions(string projectDir)
        {
            _projectDir = projectDir;
        }

        public override bool TryGetValue(string key, out string value)
        {
            if (key == "build_property.projectdir")
            {
                value = _projectDir;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}