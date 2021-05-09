using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertyChanged.SourceGenerator.UnitTests.Framework
{
    public abstract class TestsBase
    {
        private (GeneratorDriver driver, Compilation compilation, ImmutableArray<Diagnostic> diagnostics) RunDriver(
            string input,
            NullableContextOptions nullableContextOptions)
        {
            input = @"using PropertyChanged.SourceGenerator;
" + input;

            var inputCompilation = CSharpCompilation.Create("TestCompilation",
                new[] { CSharpSyntaxTree.ParseText(input) },
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(x => !x.IsDynamic && !string.IsNullOrWhiteSpace(x.Location))
                    .Select(x => MetadataReference.CreateFromFile(x.Location)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: nullableContextOptions));

            var generator = new PropertyChangedSourceGenerator();

            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);

            return (driver, outputCompilation, diagnostics);
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
            var compilationDiagnostics = compilation.GetDiagnostics().Except(generatorDiagonstics);
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
    }

    public class Expectation
    {
        public ImmutableList<DiagnosticResult> ExpectedDiagnostics { get; }
        public ImmutableList<FileExpectation> ExpectedFiles { get; }

        public Expectation()
            : this(ImmutableList<DiagnosticResult>.Empty, ImmutableList<FileExpectation>.Empty)
        {
        }

        private Expectation(ImmutableList<DiagnosticResult> expectedDiagnostics, ImmutableList<FileExpectation> expectedFiles)
        {
            this.ExpectedDiagnostics = expectedDiagnostics;
            this.ExpectedFiles = expectedFiles;
        }

        public Expectation HasFile(string name, string source, params CSharpSyntaxVisitor<SyntaxNode?>[] rewriters) =>
            this.HasFile(name, source, rewriters.AsEnumerable());

        public Expectation HasFile(string name, string source, IEnumerable<CSharpSyntaxVisitor<SyntaxNode?>> rewriters)
        {
            if (this.ExpectedFiles.Any(x => x.Name == name))
                throw new ArgumentException("Already have a file with that name", nameof(name));

            return new Expectation(this.ExpectedDiagnostics, this.ExpectedFiles.Add(
                new FileExpectation(name, source, rewriters.ToImmutableList())));
        }

        public Expectation HasDiagnostics(params DiagnosticResult[] expectediagnostics)
        {
            return new Expectation(this.ExpectedDiagnostics.AddRange(expectediagnostics), this.ExpectedFiles);
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
}
