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
            NullableContextOptions nullableContextOptions = NullableContextOptions.Disable,
            params DiagnosticResult[] diagnostics)
        {
            var (driver, compilation, generatorDiagonstics) = this.RunDriver(input, nullableContextOptions);
            DiagnosticVerifier.VerifyDiagnostics(generatorDiagonstics, diagnostics, 1); // We add 1 using statement

            var runResult = driver.GetRunResult();
            if (string.IsNullOrEmpty(expected))
            {
                Assert.AreEqual(2, runResult.GeneratedTrees.Length);
            }
            else
            {
                // 0: Attributes
                // 1: Generated file
                // 2: PropertyChangedEventArgsCache
                Assert.AreEqual(3, runResult.GeneratedTrees.Length);
                //Assert.IsEmpty(runResult.Diagnostics);

                var rootSyntaxNode = runResult.GeneratedTrees[1].GetRoot();
                if (rewriter != null)
                {
                    rootSyntaxNode = rewriter.Visit(rootSyntaxNode);
                }

                string actual = rootSyntaxNode?.ToFullString().Trim().Replace("\r\n", "\n") ?? "";
                // Strip off the comments at the top
                actual = string.Join('\n', actual.Split('\n').SkipWhile(x => x.StartsWith("//")));

                TestContext.WriteLine(actual.Replace("\"", "\"\""));

                Assert.AreEqual(expected.Trim().Replace("\r\n", "\n"), actual);
            }

            //Assert.AreEqual(2, outputCompilation.SyntaxTrees.Count());
            // We can expect compilation-level diagnostics if there are generator diagnostics: filter the known
            // bad ones out
            var compilationDiagnostics = compilation.GetDiagnostics().Except(generatorDiagonstics);
            if (generatorDiagonstics.Length > 0)
            {
                // CS0169: Field isn't used
                // CS0067: Even isn't used
                compilationDiagnostics = compilationDiagnostics.Where(x => x.Id is not ("CS0169" or "CS0067"));
            }
            Assert.IsEmpty(compilationDiagnostics, "Unexpected diagnostics:\r\n\r\n" + string.Join("\r\n", compilationDiagnostics.Select(x => x.ToString())));

        }

        protected static DiagnosticResult Diagnostic(string code, string squiggledText)
        {
            return new DiagnosticResult(code, squiggledText);
        }
    }
}
