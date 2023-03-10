using Basic.Reference.Assemblies;
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
using VerifyNUnit;
using VerifyTests;

namespace PropertyChanged.SourceGenerator.UnitTests.Framework;

public abstract class TestsBase
{
    private readonly VerifySettings verifySettings;

    public TestsBase()
    {
        this.verifySettings = new VerifySettings();
        this.verifySettings.UseExtension("cs");
        this.verifySettings.UseDirectory("../");
    }

    private Compilation CreateCompilation(string input, NullableContextOptions nullableContextOptions = NullableContextOptions.Disable, bool addAttributes = false)
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
            ReferenceAssemblies.Net50,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: nullableContextOptions));
        return inputCompilation;
    }

    private (GeneratorDriver driver, Compilation compilation, ImmutableArray<Diagnostic> diagnostics) RunDriver(
        string input,
        NullableContextOptions nullableContextOptions = NullableContextOptions.Disable)
    {
        var inputCompilation = this.CreateCompilation(input, nullableContextOptions);

        var generator = new PropertyChangedSourceGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { generator.AsSourceGenerator() }, driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, true));
        driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);
        var runResult = driver.GetRunResult();

        return (driver, outputCompilation, diagnostics);
    }

    protected TypeAnalysis Analyse(string input, string name)
    {
        var compilation = this.CreateCompilation(input, NullableContextOptions.Disable, addAttributes: true);
        var type = compilation.GetTypeByMetadataName(name);
        Assert.NotNull(type);

        var diagnostics = new DiagnosticReporter();
        var analyser = new Analyser(diagnostics, compilation, compilation.Options.NullableContextOptions, new ConfigurationParser(new TestOptionsProvider()));
        
        var analyserInput = new AnalyserInput(type!);
        foreach (var member in type!.GetMembers().Where(x => !x.IsImplicitlyDeclared))
        {
            var attributes = member.GetAttributes().Where(x => x.ToString()!.StartsWith("PropertyChanged.SourceGenerator")).ToList();
            if (attributes.Count > 0)
            {
                analyserInput.Update(member, attributes.ToImmutableArray());
            }
        }
        var inputs = new Dictionary<INamedTypeSymbol, AnalyserInput>(SymbolEqualityComparer.Default)
        {
            {  type!, analyserInput },
        };
        var typeAnalyses = analyser.Analyse(inputs, CancellationToken.None).ToList();

        DiagnosticVerifier.VerifyDiagnostics(diagnostics.GetDiagnostics(), Array.Empty<DiagnosticResult>(), 1);

        Assert.AreEqual(1, typeAnalyses.Count);
        return typeAnalyses[0];
    }

    protected static Expectation It { get; } = new Expectation();
    protected static ImmutableList<CSharpSyntaxVisitor<SyntaxNode?>> StandardRewriters { get; } = new CSharpSyntaxVisitor<SyntaxNode?>[] {
        RemovePropertiesRewriter.Instance, RemoveInpcMembersRewriter.All, RemoveDocumentationRewriter.Instance,
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
            // 2: EventArgsCache
            //Assert.AreEqual(3, runResult.GeneratedTrees.Length);
            //Assert.IsEmpty(runResult.Diagnostics);

            foreach (var expectedFile in expectation.ExpectedFiles)
            {
                var generatedTree = runResult.GeneratedTrees.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x.FilePath) == expectedFile.Name + ".g");
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

                if (expectedFile.Source != null)
                {
                    Assert.AreEqual(expectedFile.Source.Trim().Replace("\r\n", "\n"), actual);
                }
                else
                {
                    string testName = TestContext.CurrentContext.Test.Name;
                    // If it's parameterised, strip off the parameters
                    if (testName.IndexOf('(') is >= 0 and int index)
                    {
                        testName = testName.Substring(0, index);
                    }

                    Verifier.Verify(actual, this.verifySettings)
                        .UseMethodName($"{testName}_{expectedFile.Name}")
                        .GetAwaiter().GetResult();
                }
            }

            foreach (string expectedMissingFile in expectation.ExpectedMissingFiles)
            {
                var generatedTree = runResult.GeneratedTrees.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x.FilePath) == expectedMissingFile);
                Assert.Null(generatedTree, $"Unexpected file with name {expectedMissingFile}");
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
        Assert.That(member!.AlsoNotify.Select(x => x.Name), Has.Member(propertyName));
    }

    protected void AssertNotifiesFromBase(string input, string type, string memberName, string propertyName)
    {
        var analysis = this.Analyse(input, type);
        Assert.That(analysis.BaseDependsOn
            .Where(x => x.baseProperty == memberName).Select(x => x.notifyProperty.Name), Has.Member(propertyName));
    }

    protected void AssertDoesNotNotify(string input, string type, string? memberName)
    {
        var analysis = this.Analyse(input, type);
        var member = analysis.Members.FirstOrDefault(x => x.Name == memberName);
        if (member != null)
        {
            Assert.IsEmpty(member!.AlsoNotify);
        }
        Assert.IsEmpty(analysis.BaseDependsOn.Where(x => x.notifyProperty.Name == memberName));
    }
}

public class Expectation
{
    public ImmutableList<DiagnosticResult> ExpectedDiagnostics { get; }
    public ImmutableList<FileExpectation> ExpectedFiles { get; }
    public ImmutableList<string> ExpectedMissingFiles { get; }
    public ImmutableList<string> AllowedCompilationDiagnostics { get; }

    public Expectation()
        : this(
              ImmutableList<DiagnosticResult>.Empty,
              ImmutableList<FileExpectation>.Empty,
              ImmutableList<string>.Empty,
              ImmutableList<string>.Empty)
    {
    }

    private Expectation(
        ImmutableList<DiagnosticResult> expectedDiagnostics,
        ImmutableList<FileExpectation> expectedFiles,
        ImmutableList<string> expectedMissingFiles,
        ImmutableList<string> allowedCompilationDiagnostics)
    {
        this.ExpectedDiagnostics = expectedDiagnostics;
        this.ExpectedFiles = expectedFiles;
        this.ExpectedMissingFiles = expectedMissingFiles;
        this.AllowedCompilationDiagnostics = allowedCompilationDiagnostics;
    }

    public Expectation HasFile(string name, string source, params CSharpSyntaxVisitor<SyntaxNode?>[] rewriters) =>
        this.HasFile(name, source, rewriters.AsEnumerable());

    public Expectation HasFile(string name, string source, IEnumerable<CSharpSyntaxVisitor<SyntaxNode?>> rewriters)
    {
        if (this.ExpectedFiles.Any(x => x.Name == name))
            throw new ArgumentException("Already have a file with that name", nameof(name));

        return new Expectation(
            this.ExpectedDiagnostics,
            this.ExpectedFiles.Add(
            new FileExpectation(name, source, rewriters.ToImmutableList())),
            this.ExpectedMissingFiles,
            this.AllowedCompilationDiagnostics);
    }

    public Expectation HasFile(string name, params CSharpSyntaxVisitor<SyntaxNode?>[] rewriters) =>
        this.HasFile(name, rewriters.AsEnumerable());

    public Expectation HasFile(string name, IEnumerable<CSharpSyntaxVisitor<SyntaxNode?>> rewriters) =>
        this.HasFile(name, null!, rewriters);

    public Expectation HasDiagnostics(params DiagnosticResult[] expectediagnostics)
    {
        return new Expectation(
            this.ExpectedDiagnostics.AddRange(expectediagnostics),
            this.ExpectedFiles,
            this.ExpectedMissingFiles,
            this.AllowedCompilationDiagnostics);
    }

    public Expectation DoesNotHaveFile(string name)
    {
        return new Expectation(
            this.ExpectedDiagnostics,
            this.ExpectedFiles,
            this.ExpectedMissingFiles.Add(name),
            this.AllowedCompilationDiagnostics);
    }

    public Expectation AllowCompilationDiagnostics(params string[] compilationDiagnostics)
    {
        return new Expectation(
            this.ExpectedDiagnostics,
            this.ExpectedFiles,
            this.ExpectedMissingFiles,
            this.AllowedCompilationDiagnostics.AddRange(compilationDiagnostics));
    }
}

public readonly struct FileExpectation
{
    public string Name { get; }
    public string? Source { get; }
    public ImmutableList<CSharpSyntaxVisitor<SyntaxNode?>> Rewriters { get; }

    public FileExpectation(string name, string? source, ImmutableList<CSharpSyntaxVisitor<SyntaxNode?>> rewriters)
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
