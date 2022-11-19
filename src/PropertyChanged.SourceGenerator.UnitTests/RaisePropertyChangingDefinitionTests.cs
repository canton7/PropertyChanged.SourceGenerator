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
public class RaisePropertyChangingDefinitionTests : TestsBase
{
    private static readonly CSharpSyntaxVisitor<SyntaxNode?>[] rewriters = new CSharpSyntaxVisitor<SyntaxNode?>[]
    {
        RemoveInpcMembersRewriter.Changed, RemovePropertiesRewriter.Instance, RemoveDocumentationRewriter.Instance,
    };


    [Test]
    public void DoesNothingIfTypeDoesNotImplementInterface()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void GeneratesEventAndRaisePropertyChangingIfNotDefined()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanging
{
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void GeneratesRaisePropertyChangingIfNotDefined()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanging
{
    public event PropertyChangingEventHandler PropertyChanging;
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
public class Base : INotifyPropertyChanging
{
    public event PropertyChangingEventHandler PropertyChanging;
}
public partial class Derived : Base
{
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasDiagnostics(
            // (7,22): Warning INPC0028: Could not find any suitable methods to raise the PropertyChanging event defined on a base class
            // Derived
            Diagnostic("INPC0028", @"Derived").WithLocation(7, 22)
        ));
    }

    [Test]
    public void RaisesIfMethodOnBaseClassIsPrivate()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanging
{
    public event PropertyChangingEventHandler PropertyChanging;
    private void OnPropertyChanging(string name) => this.PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(name));
}
public partial class Derived : Base
{
    [Notify]
    private string _foo;
}";

        this.AssertThat(input, It.HasDiagnostics(
            // (8,22): Warning INPC029: Found one or more methods called 'OnPropertyChanging' to raise the PropertyChanging event, but they had an unrecognised signatures or were inaccessible. No PropertyChanging events will be raised from this type
            // Derived
            Diagnostic("INPC029", @"Derived").WithLocation(8, 22)
        ));
    }

    [Test]
    public void RaisesIfNonVirtualBaseMethodAndOverrideRequired()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanging
{
    public event PropertyChangingEventHandler PropertyChanging;
    protected void OnPropertyChanging(string name) => this.PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(name));
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Bar"")]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters).HasDiagnostics(
            // (6,20): Warning INPC031: Method 'OnPropertyChanging' is non-virtual. Functionality such as dependencies on base properties will not work. Please make this method virtual
            // OnPropertyChanging
            Diagnostic("INPC031", @"OnPropertyChanging").WithLocation(6, 20),

            // (10, 14): Warning INPC035: [DependsOn("Bar")] specified, but PropertyChanging events will not be raised because the method to raise PropertyChanging events 'OnPropertyChanged' cannot defined or overridden by the source generator
            // DependsOn("Bar")
            Diagnostic("INPC035", @"DependsOn(""Bar"")").WithLocation(10, 14)
        ));
    }

    [Test]
    public void DefinesVirtual()
    {
        string input = @"
using System.ComponentModel;
public partial class Derived : INotifyPropertyChanging
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void DefinesOverrideStringNoOld()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanging
{
    public event PropertyChangingEventHandler PropertyChanging;
    protected virtual void OnPropertyChanging(string name) => this.PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(name));
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void DefinesOverrideStringOld()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanging
{
    public event PropertyChangingEventHandler PropertyChanging;
    protected virtual void OnPropertyChanging(string name, object oldValue) => this.PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(name));
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void DefinesOverrideEventArgsNoOld()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanging
{
    public event PropertyChangingEventHandler PropertyChanging;
    protected virtual void OnPropertyChanging(PropertyChangingEventArgs args) => this.PropertyChanging?.Invoke(this, args);
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void DefinesOverrideEventArgsOld()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanging
{
    public event PropertyChangingEventHandler PropertyChanging;
    protected virtual void OnPropertyChanging(PropertyChangingEventArgs args, object oldValue) => this.PropertyChanging?.Invoke(this, args);
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void CallsOnPropertyNameChangingNoOld()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanging
{
    public event PropertyChangingEventHandler PropertyChanging;
    protected virtual void OnPropertyChanging(PropertyChangingEventArgs args, object oldValue) => this.PropertyChanging?.Invoke(this, args);
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
    private void OnBarChanging() { }
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void CallsOnPropertyNameChangingOld()
    {
        string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanging
{
    public event PropertyChangingEventHandler PropertyChanging;
    protected virtual void OnPropertyChanging(PropertyChangingEventArgs args, object oldValue) => this.PropertyChanging?.Invoke(this, args);
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
    private void OnBarChanging(string oldValue) { }
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
public class Base : INotifyPropertyChanging
{{
    public event PropertyChangingEventHandler PropertyChanging;
    {accessibility} virtual void OnPropertyChanging(string name) => this.PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(name));
}}
public partial class Derived : Base
{{
    [Notify, DependsOn(""Foo"")] private string _bar;
}}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }
}
