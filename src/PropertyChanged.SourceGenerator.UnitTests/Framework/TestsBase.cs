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
        private (GeneratorDriver driver, Compilation compilation, ImmutableArray<Diagnostic> diagnostics) RunDriver(string input)
        {
            input = @"using PropertyChanged.SourceGenerator;
" + input;

            var inputCompilation = CSharpCompilation.Create("TestCompilation",
                new[] { CSharpSyntaxTree.ParseText(input) },
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(x => !x.IsDynamic && !string.IsNullOrWhiteSpace(x.Location))
                    .Select(x => MetadataReference.CreateFromFile(x.Location)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new PropertyChangedSourceGenerator();

            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);

            return (driver, outputCompilation, diagnostics);
        }

        protected void AssertSource(string expected, string input, CSharpSyntaxVisitor<SyntaxNode?>? rewriter = null)
        {
            var (driver, compilation, diagnostics) = this.RunDriver(input);

            Assert.IsEmpty(diagnostics);
            //Assert.AreEqual(2, outputCompilation.SyntaxTrees.Count());
            var compilationDiagnostics = compilation.GetDiagnostics();
            Assert.IsEmpty(compilationDiagnostics, "Unexpected diagnostics:\r\n\r\n" + string.Join("\r\n", compilationDiagnostics.Select(x => x.ToString())));

            var runResult = driver.GetRunResult();
            // 0: Attributes
            // 1: Generated file
            Assert.AreEqual(2, runResult.GeneratedTrees.Length);
            Assert.IsEmpty(runResult.Diagnostics);

            var rootSyntaxNode = runResult.GeneratedTrees[1].GetRoot();
            if (rewriter != null)
            {
                rootSyntaxNode = rewriter.Visit(rootSyntaxNode);
            }

            TestContext.WriteLine(rootSyntaxNode?.ToString().Replace("\"", "\"\""));

            Assert.AreEqual(expected.Trim().Replace("\r\n", "\n"), rootSyntaxNode?.ToString().Trim().Replace("\r\n", "\n"));
        }

        protected void AssertDiagnostics(string input, params DiagnosticResult[] expected)
        {
            var (driver, compilation, diagnostics) = this.RunDriver(input);
            DiagnosticVerifier.VerifyDiagnostics(diagnostics, expected);
        }

        protected static DiagnosticResult Diagnostic(string code, string squiggledText)
        {
            return new DiagnosticResult(code, squiggledText);
        }
    }
}
