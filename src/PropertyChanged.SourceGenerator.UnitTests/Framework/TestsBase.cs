using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
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

        protected void AssertSource(
            string expected,
            string input,
            CSharpSyntaxVisitor<SyntaxNode?>? rewriter = null,
            NullableContextOptions nullableContextOptions = NullableContextOptions.Disable)
        {
            var (driver, compilation, diagnostics) = this.RunDriver(input, nullableContextOptions);

            var runResult = driver.GetRunResult();

            // 0: Attributes
            // 1: Generated file
            // 2: PropertyChangedEventArgsCache
            Assert.AreEqual(3, runResult.GeneratedTrees.Length);
            Assert.IsEmpty(runResult.Diagnostics);

            var rootSyntaxNode = runResult.GeneratedTrees[1].GetRoot();
            if (rewriter != null)
            {
                rootSyntaxNode = rewriter.Visit(rootSyntaxNode);
            }

            string actual = rootSyntaxNode?.ToFullString().Trim().Replace("\r\n", "\n") ?? "";
            // Strip off the comments at the top
            actual = string.Join('\n', actual.Split('\n').SkipWhile(x => x.StartsWith("//")));

            TestContext.WriteLine(actual.Replace("\"", "\"\""));

            Assert.IsEmpty(diagnostics, "Unexpected diagnostics:\r\n\r\n" + string.Join("\r\n", diagnostics.Select(x => x.ToString())));
            //Assert.AreEqual(2, outputCompilation.SyntaxTrees.Count());
            var compilationDiagnostics = compilation.GetDiagnostics();
            Assert.IsEmpty(compilationDiagnostics, "Unexpected diagnostics:\r\n\r\n" + string.Join("\r\n", compilationDiagnostics.Select(x => x.ToString())));

            Assert.AreEqual(expected.Trim().Replace("\r\n", "\n"), actual);
        }

        protected void AssertDiagnostics(string input, params DiagnosticResult[] expected)
        {
            var (driver, compilation, diagnostics) = this.RunDriver(input, NullableContextOptions.Enable);
            DiagnosticVerifier.VerifyDiagnostics(diagnostics, expected, 1); // We add 1 using statement
        }

        protected static DiagnosticResult Diagnostic(string code, string squiggledText)
        {
            return new DiagnosticResult(code, squiggledText);
        }
    }
}
