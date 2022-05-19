using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests;

[TestFixture]
public class RaisePropertyChangedCallTests : TestsBase
{
    [Test]
    public void GeneratesInpcInterfaceIfNotSpecified()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", RemovePropertiesRewriter.Instance));
    }

    [Test]
    public void DoesNotGenerateInpcInterfaceIfAlreadySpecified()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanged
{
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", RemovePropertiesRewriter.Instance));
    }

    [Test]
    public void FindsAndCallsMethodWithEventArgs()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs ea) =>
        PropertyChanged?.Invoke(this, ea);
}
public partial class Derived : Base
{
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("Derived"));
    }

    [Test]
    public void FindsAndCallsMethodWithStringName()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    private void NotifyPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel"));
    }

    [Test]
    public void FindsAndCallsMethodWithEventArgsAndOldAndNewValues()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    private void NotifyPropertyChanged(PropertyChangedEventArgs args, object oldValue, object newValue) =>
        PropertyChanged?.Invoke(this, args);
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel"));
    }

    [Test]
    public void FindsAndCallsMethodWithStringNameAndOldAndNewValues()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    private void NotifyPropertyChanged(string name, object oldValue, object newValue) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel"));
    }

    [Test]
    public void RaisesIfMethodFoundWithBadSignature()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel
{
    public event PropertyChangedEventHandler PropertyChanged;
    internal void OnPropertyChanged(string name, string other) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasDiagnostics(
            // (3,22): Warning INPC006: Found one or more methods called 'RaisePropertyChanged' to raise the PropertyChanged event, but they had an unrecognised signatures or were inaccessible
            // SomeViewModel
            Diagnostic("INPC006", @"SomeViewModel").WithLocation(3, 22)
        ));
    }

    [Test]
    public void PrefersMethodEarlierInListWithBadSignatureToOneLaterInListWithGoodSignature()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel
{
    public event PropertyChangedEventHandler PropertyChanged;
    internal void OnPropertyChanged(string name, string other) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private void RaisePropertyChanged(string name) { }
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasDiagnostics(
            // (3,22): Warning INPC006: Found one or more methods called 'OnPropertyChanged' to raise the PropertyChanged event, but they had an unrecognised signatures or were inaccessible
            // SomeViewModel
            Diagnostic("INPC006", @"SomeViewModel").WithLocation(3, 22)
        ));
    }

    [Test]
    public void SearchesForMethodFromTopOfTypeHierarchy()
    {
        string input = @"
using System.ComponentModel;
public class A : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void NotifyOfPropertyChange(PropertyChangedEventArgs ea) => PropertyChanged?.Invoke(this, ea);
}
public partial class B : A
{
    [Notify]
    private string _foo;
}
public partial class C : B
{
    [Notify]
    private string _bar;
    protected void OnPropertyChanged(string name) { }
}";

        this.AssertThat(input, It.HasFile("B").HasFile("C"));
    }

    [Test]
    public void RaisesIfUserDefinedOverrideFound()
    {
        string input = @"
using System.ComponentModel;
public partial class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class Derived : Base
{
    [Notify] private string _bar;
    protected override void OnPropertyChanged(string name) { }
}";

        this.AssertThat(input, It.HasDiagnostics(
            // (12,29): Warning INPC021: Method 'OnPropertyChanged' must not be overridden. Functionality such as automatic dependencies on base properties will not work. Define a method called TODO instead
            // OnPropertyChanged
            Diagnostic("INPC021", @"OnPropertyChanged").WithLocation(12, 29)));
    }

    [Test]
    public void FindsGenericBaseClasses()
    {
        // https://github.com/canton7/PropertyChanged.SourceGenerator/issues/3

        string input = @"
public partial class A<T>
{
    [Notify]
private string _foo;
}
public partial class B : A<string>
{
    [Notify]
    private string _bar;
}";
        // It doesn't generate a new RaisePropertyChanged method
        this.AssertThat(input, It.HasFile("B", RemovePropertiesRewriter.Instance));
    }
}
