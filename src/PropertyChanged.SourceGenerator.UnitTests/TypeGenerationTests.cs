using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace PropertyChanged.SourceGenerator.UnitTests;

[TestFixture]
public class TypeGenerationTests : TestsBase
{
    [Test]
    public void GeneratesNamespace()
    {
        string input = @"
using System.ComponentModel;
namespace Test.Foo
{
    public partial class SomeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        [Notify]
        private string _foo;
    }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters));
    }

    [Test]
    public void RaisesIfTypeIsNotPartialAndHasNotifyProperties()
    {
        string input = @"
using System.ComponentModel;
public class SomeViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasDiagnostics(
            // (3,14): Warning INPC002: Type 'SomeViewModel' must be partial in order for PropertyChanged.SourceGenerator to generate properties
            // SomeViewModel
            Diagnostic("INPC002", @"SomeViewModel").WithLocation(3, 14)
        ));
    }

    [Test]
    public void HandlesBadlyNamedGenericTypes()
    {
        string input = @"
public partial class SomeViewModel<@class>
{
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters));
    }

    [Test]
    public void HandlesGenericTypesWithConstraints()
    {
        string input = @"
public partial class SomeViewModel<T> where T : class
{
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters));
    }

    [Test]
    public void DoesNotGenerateEmptyCache()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private int _foo;
    protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, null);
}";
        this.AssertThat(input, It.DoesNotHaveFile("EventArgsCache"));
    }

    [Test]
    public void HandlesPartialNestedTypes()
    {
        // https://github.com/canton7/PropertyChanged.SourceGenerator/issues/2

        string input = @"
partial class A
{
    partial class B
    {
        partial class C
        {
            [Notify] private string _field;
        }
    }
}";

        this.AssertThat(input, It.HasFile("C", StandardRewriters));
    }

    [Test]
    public void ComplainsIfOuterTypeIsNotPartial()
    {
        string input = @"
public class A
{
    partial class C
    {
        [Notify] private string _field;
    }
}";

        this.AssertThat(input, It.HasDiagnostics(
            // (2,14): Warning INPC020: Type 'A' must be partial in order for PropertyChanged.SourceGenerator to generate properties for inner type 'C'
            // A
            Diagnostic("INPC020", @"A").WithLocation(2, 14)));
    }

    [Test]
    public void HandlesTwoClassesSameNameDifferentNamespaces()
    {
        string input = @"
namespace NS1
{
    partial class SomeViewModel
    {
        [Notify] string _a;
    }
}

namespace NS2
{
    partial class SomeViewModel
    {
        [Notify] string _a;
    }
}";

        this.AssertThat(input,
            It.HasFile("SomeViewModel", StandardRewriters)
                .HasFile("SomeViewModel2", StandardRewriters));
    }
}
