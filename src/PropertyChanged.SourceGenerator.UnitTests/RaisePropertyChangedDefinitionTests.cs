using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests;

[TestFixture]
public class RaisePropertyChangedDefinitionTests : TestsBase
{
    private static readonly CSharpSyntaxVisitor<SyntaxNode?>[] rewriters = new CSharpSyntaxVisitor<SyntaxNode?>[]
    {
        RemovePropertiesRewriter.Instance, RemoveInpcMembersRewriter.CommentsOnly
    };

    [Test]
    public void GeneratesEventAndRaisePropertyChangedIfNotDefined()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanged
{
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void GeneratesRaisePropertyChangedIfNotDefined()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void RaisesIfEventButNoRaiseMethodOnBaseClass()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
}
public partial class Derived : Base
{
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasDiagnostics(
            // (7,22): Warning INPC007: Could not find any suitable methods to raise the PropertyChanged event defined on a base class
            // Derived
            Diagnostic("INPC007", @"Derived").WithLocation(7, 22)
        ));
    }

    [Test]
    public void RaisesIfMethodOnBaseClassIsPrivate()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public partial class Derived : Base
{
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasDiagnostics(
            // (8,22): Warning INPC006: Found one or more methods called 'RaisePropertyChanged' to raise the PropertyChanged event, but they had an unrecognised signatures or were inaccessible
            // Derived
            Diagnostic("INPC006", @"Derived").WithLocation(8, 22)
        ));
    }

    [Test]
    public void RaisesIfNonVirtualBaseMethodAndOverrideRequired()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Bar"")]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("Derived", RemovePropertiesRewriter.Instance).HasDiagnostics(
            // (6,20): Warning INPC022: Method 'OnPropertyChanged' is non-virtual. Functionality such as dependencies on base properties will not work. Please make this method virtual
            // OnPropertyChanged
            Diagnostic("INPC022", @"OnPropertyChanged").WithLocation(6, 20),

            // (10,14): Warning INPC023: [DependsOn("Bar")] specified, but this will not be raised because the method to raise PropertyChanged events 'OnPropertyChanged' cannot defined or overridden by the source generator
            // DependsOn("Bar")
            Diagnostic("INPC023", @"DependsOn(""Bar"")").WithLocation(10, 14)
        ));
    }

    [Test]
    public void DefinesVirtual()
    {
        string input = @"
public partial class Derived
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void DefinesOverrideStringNoOldAndNew()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string name) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void DefinesOverrideStringOldAndNew()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string name, object oldValue, object newValue) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void DefinesOverrideEventArgsNoOldAndNew()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs args) => this.PropertyChanged?.Invoke(this, args);
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void DefinesOverrideEventArgsOldAndNew()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs args, object oldValue, object newValue) => this.PropertyChanged?.Invoke(this, args);
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void CallsOnPropertyNameChangedNoOldAndNew()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs args, object oldValue, object newValue) => this.PropertyChanged?.Invoke(this, args);
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
    private void OnBarChanged() { }
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void CallsOnPropertyNameChangedOldAndNew()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs args, object oldValue, object newValue) => this.PropertyChanged?.Invoke(this, args);
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
    private void OnBarChanged(string oldValue, string newValue) { }
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [TestCase("public")]
    [TestCase("protected internal")]
    [TestCase("protected")]
    [TestCase("internal")]
    [TestCase("private protected")]
    public void CopiesBaseMethodAccessibility(string accessibility)
    {
        string input = @$"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{{
    public event PropertyChangedEventHandler PropertyChanged;
    {accessibility} virtual void OnPropertyChanged(string name) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}}
public partial class Derived : Base
{{
    [Notify, DependsOn(""Foo"")] private string _bar;
}}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void HandlesSealedClass()
    {
        string input = @"
public sealed partial class SomeViewModel
{
    [Notify] string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }
}
