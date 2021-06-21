using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.Analysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PropertyChanged.SourceGenerator.UnitTests.Framework
{
    public abstract class TestsBase
    {
        private Compilation CreateCompilation(string input, NullableContextOptions nullableContextOptions, bool addAttributes = false)
        {
            input = @"using PropertyChanged.SourceGenerator;
" + input;
            var syntaxTrees = new List<SyntaxTree>() { CSharpSyntaxTree.ParseText(input) };
            if (addAttributes)
            {
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(StringConstants.Attributes));
            }

            var inputCompilation = CSharpCompilation.Create("TestCompilation",
                syntaxTrees,
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(x => !x.IsDynamic && !string.IsNullOrWhiteSpace(x.Location))
                    .Select(x => MetadataReference.CreateFromFile(x.Location)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: nullableContextOptions));
            return inputCompilation;
        }

        private (GeneratorDriver driver, Compilation compilation, ImmutableArray<Diagnostic> diagnostics) RunDriver(
            string input,
            NullableContextOptions nullableContextOptions)
        {
            var inputCompilation = this.CreateCompilation(input, nullableContextOptions);

            var generator = new PropertyChangedSourceGenerator();

            GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { generator }, optionsProvider: new TestOptionsProvider());
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);

            return (driver, outputCompilation, diagnostics);
        }

        protected TypeAnalysis Analyse(string input, string name)
        {
            var compilation = this.CreateCompilation(input, NullableContextOptions.Disable, addAttributes: true);
            var type = compilation.GetTypeByMetadataName(name);
            Assert.NotNull(type);

            var diagnostics = new DiagnosticReporter();
            var analyser = new Analyser(diagnostics, compilation, new ConfigurationParser(new TestOptionsProvider(), diagnostics));
            var typeAnalyses = analyser.Analyse(new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { type! }).ToList();

            DiagnosticVerifier.VerifyDiagnostics(diagnostics.Diagnostics, Array.Empty<DiagnosticResult>(), 1);

            Assert.AreEqual(1, typeAnalyses.Count);
            return typeAnalyses[0];
        }

        protected static Expectation It { get; } = new Expectation();
        protected static ImmutableList<CSharpSyntaxVisitor<SyntaxNode?>> StandardRewriters { get; } = new CSharpSyntaxVisitor<SyntaxNode?>[] {
            RemovePropertiesRewriter.Instance, RemoveInpcMembersRewriter.Instance
        }.ToImmutableList();

        protected void AssertThat(
            string input,
            Expectation expectation,
            NullableContextOptions nullableContextOptions = NullableContextOptions.Disable)
        {
            var (driver, compilation, generatorDiagonstics) = this.RunDriver(input, nullableContextOptions);
            DiagnosticVerifier.VerifyDiagnostics(generatorDiagonstics, expectation.ExpectedDiagnostics.ToArray(), 1); // We add 1 using statement

            if (expectation.ExpectedFiles.Count > 0)
            {
                var runResult = driver.GetRunResult();

                // 0: Attributes
                // 1: Generated file
                // 2: PropertyChangedEventArgsCache
                //Assert.AreEqual(3, runResult.GeneratedTrees.Length);
                //Assert.IsEmpty(runResult.Diagnostics);

                foreach (var expectedFile in expectation.ExpectedFiles)
                {
                    var generatedTree = runResult.GeneratedTrees.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x.FilePath) == expectedFile.Name);
                    Assert.NotNull(generatedTree, $"No output file with name {expectedFile.Name}");

                    var rootSyntaxNode = generatedTree!.GetRoot();
                    foreach (var rewriter in expectedFile.Rewriters)
                    {
                        rootSyntaxNode = rewriter.Visit(rootSyntaxNode);
                    }

                    string actual = rootSyntaxNode?.ToFullString().Trim().Replace("\r\n", "\n") ?? "";
                    // Strip off the comments at the top
                    actual = string.Join('\n', actual.Split('\n').SkipWhile(x => x.StartsWith("//")));

                    TestContext.WriteLine(actual.Replace("\"", "\"\""));

                    Assert.AreEqual(expectedFile.Source.Trim().Replace("\r\n", "\n"), actual);
                }
            }

            //Assert.AreEqual(2, outputCompilation.SyntaxTrees.Count());
            // We can expect compilation-level diagnostics if there are generator diagnostics: filter the known
            // bad ones out
            var compilationDiagnostics = compilation.GetDiagnostics().Except(generatorDiagonstics)
                .Where(x => !expectation.AllowedCompilationDiagnostics.Contains(x.Id));
            if (generatorDiagonstics.Length > 0)
            {
                // CS0169: Field isn't used
                // CS0067: Event isn't used
                compilationDiagnostics = compilationDiagnostics.Where(x => x.Id is not ("CS0169" or "CS0067"));
            }
            Assert.IsEmpty(compilationDiagnostics, "Unexpected diagnostics:\r\n\r\n" + string.Join("\r\n", compilationDiagnostics.Select(x => x.ToString())));
        }

        protected static DiagnosticResult Diagnostic(string code, string squiggledText)
        {
            return new DiagnosticResult(code, squiggledText);
        }

        protected void AssertNotifies(string input, string type, string memberName, string propertyName)
        {
            var analysis = this.Analyse(input, type);
            var member = analysis.Members.FirstOrDefault(x => x.Name == memberName);
            Assert.NotNull(member);
            Assert.That(member!.AlsoNotify.Select(x => x.Name), Is.EquivalentTo(new[] { propertyName }));
        }

        protected void AssertDoesNotNotify(string input, string type, string memberName)
        {
            var analysis = this.Analyse(input, type);
            var member = analysis.Members.FirstOrDefault(x => x.Name == memberName);
            Assert.NotNull(member);
            Assert.IsEmpty(member!.AlsoNotify);
        }
    }

    public class Expectation
    {
        public ImmutableList<DiagnosticResult> ExpectedDiagnostics { get; }
        public ImmutableList<FileExpectation> ExpectedFiles { get; }
        public ImmutableList<string> AllowedCompilationDiagnostics { get; }

        public Expectation()
            : this(ImmutableList<DiagnosticResult>.Empty, ImmutableList<FileExpectation>.Empty, ImmutableList<string>.Empty)
        {
        }

        private Expectation(
            ImmutableList<DiagnosticResult> expectedDiagnostics,
            ImmutableList<FileExpectation> expectedFiles,
            ImmutableList<string> allowedCompilationDiagnostics)
        {
            this.ExpectedDiagnostics = expectedDiagnostics;
            this.ExpectedFiles = expectedFiles;
            this.AllowedCompilationDiagnostics = allowedCompilationDiagnostics;
        }

        public Expectation HasFile(string name, string source, params CSharpSyntaxVisitor<SyntaxNode?>[] rewriters) =>
            this.HasFile(name, source, rewriters.AsEnumerable());

        public Expectation HasFile(string name, string source, IEnumerable<CSharpSyntaxVisitor<SyntaxNode?>> rewriters)
        {
            if (this.ExpectedFiles.Any(x => x.Name == name))
                throw new ArgumentException("Already have a file with that name", nameof(name));

            return new Expectation(this.ExpectedDiagnostics, this.ExpectedFiles.Add(
                new FileExpectation(name, source, rewriters.ToImmutableList())), this.AllowedCompilationDiagnostics);
        }

        public Expectation HasDiagnostics(params DiagnosticResult[] expectediagnostics)
        {
            return new Expectation(this.ExpectedDiagnostics.AddRange(expectediagnostics), this.ExpectedFiles, this.AllowedCompilationDiagnostics);
        }

        public Expectation AllowCompilationDiagnostics(params string[] compilationDiagnostics)
        {
            return new Expectation(this.ExpectedDiagnostics, this.ExpectedFiles, this.AllowedCompilationDiagnostics.AddRange(compilationDiagnostics));
        }
    }

    public struct FileExpectation
    {
        public string Name { get; }
        public string Source { get; }
        public ImmutableList<CSharpSyntaxVisitor<SyntaxNode?>> Rewriters { get; }

        public FileExpectation(string name, string source, ImmutableList<CSharpSyntaxVisitor<SyntaxNode?>> rewriters)
        {
            this.Name = name;
            this.Source = source;
            this.Rewriters = rewriters;
        }
    }

    public class TestConfigOptions : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        {
            value = null;
            return false;
        }
    }

    public class TestOptionsProvider : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions => new TestConfigOptions();

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestConfigOptions();
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new TestConfigOptions();
    }
}
