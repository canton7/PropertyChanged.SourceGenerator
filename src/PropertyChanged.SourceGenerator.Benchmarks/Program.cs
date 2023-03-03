
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using BenchmarkDotNet.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PropertyChanged.SourceGenerator;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

[EtwProfiler]
public class Benckmarks : TestsBase
{
    private CSharpGeneratorDriver driver = null!;
    private Compilation selfGeneratedCompilation = null!;
    private Compilation baseClassCompilation = null!;

    [GlobalSetup]
    public void SetUp()
    {
        this.selfGeneratedCompilation = this.CreateCompilation(SelfGeneratedInput);
        this.baseClassCompilation = this.CreateCompilation(BaseClassInput);

        var generator = new PropertyChangedSourceGenerator();
        this.driver = CSharpGeneratorDriver.Create(new[] { generator.AsSourceGenerator() }, driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, true));
    }


    [Benchmark]
    public void SelfGenerated() => this.driver.RunGenerators(this.selfGeneratedCompilation);

    [Benchmark]
    public void BaseClass() => this.driver.RunGenerators(this.baseClassCompilation);

    private const string SelfGeneratedInput = """
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
        BenchmarkRunner.Run<Benckmarks>();
    }
}