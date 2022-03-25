using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests;

[TestFixture]
public class DependsOnTests : TestsBase
{
    [Test]
    public void NotifiesDependsOnProperty()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;

    [DependsOn(""Foo"")]
    public string Bar { get; set; }
}";
        this.AssertNotifies(input, "SomeViewModel", "Foo", "Bar");
    }

    [Test]
    public void NotifiesGeneratedDependsOnProperty()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;

    [Notify]
    [DependsOn(""Foo"")]
    private string _bar;
}";
        this.AssertNotifies(input, "SomeViewModel", "Foo", "Bar");
    }

    [Test]
    public void NotifiesPropertyWhichDoesNotExist()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    [DependsOn(""Foo"")]
    private string _bar
}";
        this.AssertNotifiesFromBase(input, "SomeViewModel", "Foo", "Bar");
    }

    [Test]
    public void RaisesIfAppliedToFieldWithoutNotify()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;

    [DependsOn(""Foo"")]
    private string _bar;
}";

        this.AssertThat(input, It.HasDiagnostics(
            // (7,6): Warning INPC011: [DependsOn] must only be applied to fields which also have [Notify]
            // DependsOn("Foo")
            Diagnostic("INPC011", @"DependsOn(""Foo"")").WithLocation(7, 6)
        ));
    }

    [Test]
    public void IgnoresEmptyAndNullNames()
    {
        string input = @"
public partial class SomeViewModel
{
    [DependsOn("""", null)]
    private string _foo;
}";
        this.AssertDoesNotNotify(input, "SomeViewModel", "Foo");
    }

    [Test]
    public void RaisesIfPropertyChangedMethodIsSpecifiedByUser()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    [Notify, DependsOn(""Foo"")]
    private string _bar;
}";
        this.AssertThat(input, It.HasDiagnostics(
            // (8,14): Warning INPC023: [DependsOn("Foo")] specified, but this will not be raised because the method to raise PropertyChanged events 'OnPropertyChanged' cannot defined or overridden by the source generator
            // DependsOn("Foo")
            Diagnostic("INPC023", @"DependsOn(""Foo"")").WithLocation(8, 14)));
    }

    [Test]
    public void RaisesIfPropertyChangedMethodIsPrivate()
    {
        string input = @"
using System.ComponentModel;
public partial class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")]
    private string _bar;
}";
        this.AssertThat(input, It.HasDiagnostics(
            // (6,20): Warning INPC022: Method 'OnPropertyChanged' is non-virtual. Functionality such as dependencies on base properties will not work. Please make this method virtual
            // OnPropertyChanged
            Diagnostic("INPC022", @"OnPropertyChanged").WithLocation(6, 20),

            // (10,14): Warning INPC023: [DependsOn("Foo")] specified, but this will not be raised because the method to raise PropertyChanged events 'OnPropertyChanged' cannot defined or overridden by the source generator
            // DependsOn("Foo")
            Diagnostic("INPC023", @"DependsOn(""Foo"")").WithLocation(10, 14)
        ));
    }
}
