using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests;

[TestFixture]
public class OnPropertyNameChangedTests : TestsBase
{
    [Test]
    public void GenerateParameterlessRaise()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public void OnFooChanged() { }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.All));
    }

    [Test]
    public void GeneratesOldAndNewWithMatchingDataType()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public void OnFooChanged(string oldValue, string newValue) { }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.All));
    }

    [Test]
    public void GeneratesOldAndNewWithParentDataType()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public void OnFooChanged(object oldValue, object newValue) { }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.All));
    }

    [Test]
    public void DoesNotMatchMethodWithDifferingParameterTypes()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public void OnFooChanged(object oldValue, string newValue) { }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.All)
            .HasDiagnostics(
                // (6,17): Warning INPC013: Found one or more On{PropertyName}Changed methods called 'OnFooChanged' for property 'Foo', but none had the correct signature, or were inaccessible. Skipping
                // OnFooChanged
                Diagnostic("INPC013", @"OnFooChanged").WithLocation(6, 17)
            ));
    }

    [Test]
    public void GeneratesAlsoNotifyCallableParameterless()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
    public int Bar { get; }
    private void OnBarChanged() { }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.All));
    }

    [Test]
    public void GeneratesAlsoNotifyCallableParameters()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
    public int Bar { get; }
    private void OnBarChanged(int oldValue, int newValue) { }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.All));
    }
    
    [Test]
    public void GeneratesAlsoNotifyOnBaseClass()
    {
        string input = @"
public partial class Base
{
    public int Bar { get; }
    protected void OnBarChanged(int oldValue, int newValue) { }
}
public partial class Derived : Base
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
}";

        this.AssertThat(input, It.HasFile("Derived", RemoveInpcMembersRewriter.All));
    }

    [Test]
    public void DoesNotGenerateAlsoNotifyWithPropertyOnBaseClassAndMethodOnDerived()
    {
        string input = @"
public partial class Base
{
    public int Bar { get; }
}
public partial class Derived : Base
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
    private void OnBarChanged(int oldValue, int newValue) { }
}";

        this.AssertThat(input, It.HasFile("Derived", RemoveInpcMembersRewriter.All));
    }

    [Test]
    public void DoesNotCallInaccessibleAlsoNotifyOnBaseClass()
    {
        string input = @"
public partial class Base
{
    public int Bar { get; }
    private void OnBarChanged(int oldValue, int newValue) { }
}
public partial class Derived : Base
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
}";

        this.AssertThat(input, It.HasFile("Derived", RemoveInpcMembersRewriter.All)
            .HasDiagnostics(
                // (5,18): Warning INPC013: Found one or more On{PropertyName}Changed methods called 'OnBarChanged' for property 'Bar', but none had the correct signature, or were inaccessible. Skipping
                // OnBarChanged
                Diagnostic("INPC013", @"OnBarChanged").WithLocation(5, 18)
            ));
    }

    [Test]
    public void DoesNotGenerateAlsoNotifyNonCallable()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.All)
            .HasDiagnostics(
                // (4,14): Warning INPC009: Unable to find a property called 'Bar' on this type or its base types. This event will still be raised
                // AlsoNotify("Bar")
                Diagnostic("INPC009", @"AlsoNotify(""Bar"")").WithLocation(4, 14)
            ));
    }

    [Test]
    public void GeneratesDependsOn()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private int _foo;
    [DependsOn(""Foo"")]
    public int Bar { get; }
    private void OnBarChanged(int oldValue, int newValue) { }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.All));
    }

    [Test]
    public void GeneratesAutoDependsOn()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private int _foo;
    public int Bar => this.Foo + 2;
    public string Baz => $""Test: {Bar}"";
    private void OnBarChanged(int oldValue, int newValue) { }
    private void OnBazChanged(string oldValue, string newValue) { }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.All));
    }
}
