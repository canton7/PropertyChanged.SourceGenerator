using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests;

[TestFixture]
public class AutoDependsOnTests : TestsBase
{
    [Test]
    public void HandlesSimpleNonQualifiedAccess()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public string FullName => Foo;
}";
        this.AssertNotifies(input, "SomeViewModel", "Foo", "FullName");
    }

    [Test]
    public void HandlesSimpleQualifiedAccess()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public string FullName => this.Foo;
}";
        this.AssertNotifies(input, "SomeViewModel", "Foo", "FullName");
    }

    [Test]
    public void IgnoresAccessOnAnotherInstance()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public string FullName => new SomeViewModel().Foo;
}";
        this.AssertDoesNotNotify(input, "SomeViewModel", "Foo");
    }

    [Test]
    public void HandlesReturnStatement()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public string FullName { get { return Foo; } }
}";
        this.AssertNotifies(input, "SomeViewModel", "Foo", "FullName");
    }

    [Test]
    public void HandlesStringConcatenation()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public string FullName => ""Mr "" + Foo;
}";
        this.AssertNotifies(input, "SomeViewModel", "Foo", "FullName");
    }

    [Test]
    public void HandlesPropertyAccess()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public int Length => Foo.Length;
}";
        this.AssertNotifies(input, "SomeViewModel", "Foo", "Length");
    }


    [Test]
    public void HandlesQualifiedPropertyAccess()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public int Length => this.Foo.Length;
}";
        this.AssertNotifies(input, "SomeViewModel", "Foo", "Length");
    }

    [Test]
    public void HandlesMethodInvocation()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public bool Contains => Foo.Contains(""Bar"");
}";
        this.AssertNotifies(input, "SomeViewModel", "Foo", "Contains");
    }

    [Test]
    public void HandlesQualifiedMethodInvocation()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public bool Contains => this.Foo.Contains(""Bar"");
}";
        this.AssertNotifies(input, "SomeViewModel", "Foo", "Contains");
    }

    [Test]
    public void IgnoresAssignment()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public string Bar
    {
        get
        {
            Foo = ""Test"";
            this.Foo = ""Test2"";
            return ""Bar"";
        }
    }
}";
        this.AssertDoesNotNotify(input, "SomeViewModel", "Foo");
    }

    [Test]
    public void IgnoresIncrements()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private int _foo;
    public int Bar => Foo++ + Foo-- + ++Foo + --Foo + this.Foo++;
}";
        this.AssertDoesNotNotify(input, "SomeViewModel", "Foo");
    }

    [Test]
    public void DisablesIfDependsOnSpecified()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private int _foo;
    [Notify]
    private int _bar;
    [DependsOn(""Foo"")]
    public int Thing => Bar;
}";

        this.AssertNotifies(input, "SomeViewModel", "Foo", "Thing");
        this.AssertDoesNotNotify(input, "SomeViewModel", "Bar");
    }

    [Test]
    public void ResolvesAutoDependsOnRecursively()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public string Baz => this.Bar;
    public string Bar => this.Foo;
}";

        this.AssertNotifies(input, "SomeViewModel", "Foo", "Bar");
        this.AssertNotifies(input, "SomeViewModel", "Foo", "Baz");

    }
}
