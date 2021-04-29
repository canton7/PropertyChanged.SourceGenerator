using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertyChanged.SourceGenerator.UnitTests
{
    public abstract class TestsBase
    {
        protected void AssertSource(string input)
        {
            var inputCompilation = CSharpCompilation.Create("TestCompilation",
                new[] { CSharpSyntaxTree.ParseText(input) },
                null,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            var generator = new PropertyChangedSourceGenerator();

            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);

            Assert.IsEmpty(diagnostics);
            Assert.Equals(2, outputCompilation.SyntaxTrees.Count());
            Assert.IsEmpty(outputCompilation.GetDiagnostics());

            var runResult = driver.GetRunResult();
            Assert.AreEqual(1, runResult.GeneratedTrees.Length);
            Assert.IsEmpty(runResult.Diagnostics);

            var generatorResult = runResult.Results[0];
            Assert.AreEqual(generator, generatorResult.Generator);
            Assert.IsEmpty(generatorResult.Diagnostics);
            Assert.AreEqual(1, generatorResult.GeneratedSources.Length);
            Assert.IsNull(generatorResult.Exception);
        }
    }
}
