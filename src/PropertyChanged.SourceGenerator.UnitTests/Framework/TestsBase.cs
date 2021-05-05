using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertyChanged.SourceGenerator.UnitTests.Framework
{
    public abstract class TestsBase
    {
        protected void AssertSource(string expected, string input)
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

            Assert.IsEmpty(diagnostics);
            //Assert.AreEqual(2, outputCompilation.SyntaxTrees.Count());
            // TODO: Might want to assert on this?
            Assert.IsEmpty(outputCompilation.GetDiagnostics());

            var runResult = driver.GetRunResult();
            // 0: Attributes
            // 1: Generated file
            Assert.AreEqual(2, runResult.GeneratedTrees.Length);
            Assert.IsEmpty(runResult.Diagnostics);

            TestContext.WriteLine(runResult.GeneratedTrees[1].ToString().Replace("\"", "\"\""));

            Assert.AreEqual(expected.Trim(), runResult.GeneratedTrees[1].GetRoot().ToString().Trim());
        }
    }
}
