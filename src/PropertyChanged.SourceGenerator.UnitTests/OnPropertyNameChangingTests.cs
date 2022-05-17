using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests;

[TestFixture]
public class OnPropertyNameChangingTests : TestsBase
{
    private static readonly CSharpSyntaxVisitor<SyntaxNode?>[] rewriters = new CSharpSyntaxVisitor<SyntaxNode?>[]
    {
        RemoveInpcMembersRewriter.All,
    };

    [Test]
    public void GenerateParameterlessRaise()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanging
{
    [Notify]
    private string _foo;
    public void OnFooChanging() { }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void GeneratesOldWithMatchingDataType()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanging
{
    [Notify]
    private string _foo;
    public void OnFooChanging(string oldValue) { }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void GeneratesOldAndNewWithParentDataType()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanging
{
    [Notify]
    private string _foo;
    public void OnFooChanging(object oldValue) { }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void GeneratesAlsoNotifyCallableParameterless()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanging
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
    public int Bar { get; }
    private void OnBarChanging() { }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void GeneratesAlsoNotifyCallableParameters()
    {
        string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanging
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
    public int Bar { get; }
    private void OnBarChanging(int oldValue) { }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void GeneratesAlsoNotifyOnBaseClass()
    {
        string input = @"
using System.ComponentModel;
public partial class Base : INotifyPropertyChanging
{
    [Notify]
    private int _bar;
    protected void OnBarChanging(int oldValue) { }
}
public partial class Derived : Base
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void DoesNotGenerateAlsoNotifyWithPropertyOnBaseClassAndMethodOnDerived()
    {
        string input = @"
using System.ComponentModel;
public partial class Base : INotifyPropertyChanging
{
    [Notify]
    private int _bar;
}
public partial class Derived : Base
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
    private void OnBarChanging(int oldValue) { }
}";

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    //    [Test]
    //    public void DoesNotCallInaccessibleAlsoNotifyOnBaseClass()
    //    {
    //        string input = @"
    //public partial class Base
    //{
    //    public int Bar { get; }
    //    private void OnBarChanged(int oldValue, int newValue) { }
    //}
    //public partial class Derived : Base
    //{
    //    [Notify, AlsoNotify(""Bar"")]
    //    private int _foo;
    //}";

    //        this.AssertThat(input, It.HasFile("Derived", RemoveInpcMembersRewriter.Instance)
    //            .HasDiagnostics(
    //                // (5,18): Warning INPC013: Found one or more On{PropertyName}Changed methods called 'OnBarChanged' for property 'Bar', but none had the correct signature, or were inaccessible. Skipping
    //                // OnBarChanged
    //                Diagnostic("INPC013", @"OnBarChanged").WithLocation(5, 18)
    //            ));
    //    }

    //    [Test]
    //    public void DoesNotGenerateAlsoNotifyNonCallable()
    //    {
    //        string input = @"
    //public partial class SomeViewModel
    //{
    //    [Notify, AlsoNotify(""Bar"")]
    //    private int _foo;
    //}";

    //        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.Instance)
    //            .HasDiagnostics(
    //                // (4,14): Warning INPC009: Unable to find a property called 'Bar' on this type or its base types. This event will still be raised
    //                // AlsoNotify("Bar")
    //                Diagnostic("INPC009", @"AlsoNotify(""Bar"")").WithLocation(4, 14)
    //            ));
    //    }

    //    [Test]
    //    public void GeneratesDependsOn()
    //    {
    //        string input = @"
    //public partial class SomeViewModel
    //{
    //    [Notify]
    //    private int _foo;
    //    [DependsOn(""Foo"")]
    //    public int Bar { get; }
    //    private void OnBarChanged(int oldValue, int newValue) { }
    //}";

    //        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.Instance));
    //    }

    //    [Test]
    //    public void GeneratesAutoDependsOn()
    //    {
    //        string input = @"
    //public partial class SomeViewModel
    //{
    //    [Notify]
    //    private int _foo;
    //    public int Bar => this.Foo + 2;
    //    public string Baz => $""Test: {Bar}"";
    //    private void OnBarChanged(int oldValue, int newValue) { }
    //    private void OnBazChanged(string oldValue, string newValue) { }
    //}";

    //        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.Instance));
    //    }
}
