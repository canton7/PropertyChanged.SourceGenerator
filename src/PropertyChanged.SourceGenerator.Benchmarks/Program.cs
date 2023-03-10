
using Basic.Reference.Assemblies;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using BenchmarkDotNet.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PropertyChanged.SourceGenerator;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

[EtwProfiler]
public class Benchmarks
{
    private (GeneratorDriver driver, Compilation compilation) selfGeneratedCompilation = default;
    private (GeneratorDriver driver, Compilation compilation) baseClassCompilation = default;

    [GlobalSetup]
    public void SetUp()
    {
        var generator = new PropertyChangedSourceGenerator();

        this.selfGeneratedCompilation = Create(SelfGeneratedInput);
        this.baseClassCompilation = Create(BaseClassInput);

        (GeneratorDriver driver, Compilation compilation) Create(string input)
        {
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { generator.AsSourceGenerator() }, driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, true));

            // Run once first with an empty syntax tree, so that FAWMN gets a chance to set itself up, without the cost of that being counted
            var inputCompilation = CSharpCompilation.Create("TestCompilation",
                new[] { CSharpSyntaxTree.ParseText(input) },
                ReferenceAssemblies.Net50,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Disable));

            driver = driver.RunGenerators(inputCompilation);

            return (driver, inputCompilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("")));
        }
    }

    private void Run((GeneratorDriver driver, Compilation compilation) pair)
    {
        pair.driver.RunGenerators(pair.compilation);
    }

    [Benchmark]
    public void SelfGenerated() => this.Run(this.selfGeneratedCompilation);

    [Benchmark]
    public void BaseClass() => this.Run(this.baseClassCompilation);

    public const string SelfGeneratedInput = """
        using PropertyChanged.SourceGenerator;
        public partial class A1
        {
            [Notify] private string _fooA;
            [Notify] private string _barA;
            [Notify] private int _bazA;
        }
        public partial class B1
        {
            [Notify] private string _fooB;
            [Notify] private string _barB;
            [Notify] private int _bazB;

            private void OnBazBChanged(int oldValue, int newValue) { }
            private void AnyProperyChanged(string propertyName, object oldValue, object newValue) { }
        }
        public partial class C1
        {
            [Notify] private string _fooC;
            [Notify] private string _barC;
            [Notify] private int _bazC;
        }
        public partial class C2 : C1
        {
            [Notify] private string _subC1;
            [Notify] private int _subC2;
            [Notify] private int _subC3;
            [Notify] private double _subC4;
            [Notify] private decumal _subC5;
            [Notify] private A1 _subC6;
        }
        """;

    private const string BaseClassInput = """
        using PropertyChanged.SourceGenerator;
        using System.ComponentModel;
        public class PropertyChangedBase : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public partial class A1 : PropertyChangedBase
        {
            [Notify] private string _fooA;
            [Notify] private string _barA;
            [Notify] private int _bazA;
        }
        public partial class B1 : PropertyChangedBase
        {
            [Notify] private string _fooB;
            [Notify] private string _barB;
            [Notify] private int _bazB;
        
            private void OnBazBChanged(int oldValue, int newValue) { }
            private void AnyProperyChanged(string propertyName, object oldValue, object newValue) { }
        }
        public partial class C1 : PropertyChangedBase
        {
            [Notify] private string _fooC;
            [Notify] private string _barC;
            [Notify] private int _bazC;
        }
        public partial class C2 : C1
        {
            [Notify] private string _subC1;
            [Notify] private int _subC2;
            [Notify] private int _subC3;
            [Notify] private double _subC4;
            [Notify] private decumal _subC5;
            [Notify] private A1 _subC6;
        }
        """;
}

public class Program
{
    public static void Main()
    {
        BenchmarkRunner.Run<Benchmarks>();
    }
}