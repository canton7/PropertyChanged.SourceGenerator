using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests;

[TestFixture]
public class PropertyNameTests : TestsBase
{
    [Test]
    public void UsesExplicitNameIfGiven()
    {
        string input = """
            public partial class SomeViewModel
            {
                [Notify("Prop")]
                private string _foo;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters));
    }

    [Test]
    public void ReportsNameCollisionInferred()
    {
        string input = """
            public partial class SomeViewModel
            {
                [Notify]
                private string _foo;
                public string Foo;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters)
            .HasDiagnostics(
            // (4,20): Warning INPC003: Attempted to generate property 'Foo' for member '_foo', but a member with that name already exists. Skipping this property
            // _foo
            Diagnostic("INPC003", @"_foo").WithLocation(4, 20)
        ));
    }

    [Test]
    public void ReportsNameCollisionExplicit()
    {
        string input = """
            public partial class SomeViewModel
            {
                [Notify("Prop")]
                private string _foo;
                public string Prop;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters)
            .HasDiagnostics(
            // (4,20): Warning INPC003: Attempted to generate property 'Prop' for member '_foo', but a member with that name already exists. Skipping this property
            // _foo
            Diagnostic("INPC003", @"_foo").WithLocation(4, 20)
        ));
    }

    [Test]
    public void ReportsNameCollisionInBaseClass()
    {
        string input = """
            public partial class SomeViewModelBase
            {
                public string Prop;
            }
            public partial class SomeViewModel : SomeViewModelBase
            {
                [Notify("Prop")]
                private string _foo;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters)
            .HasDiagnostics(
            // (8,20): Warning INPC003: Attempted to generate property 'Prop' for member '_foo', but a member with that name already exists. Skipping this property
            // _foo
            Diagnostic("INPC003", @"_foo").WithLocation(8, 20)
        ));
    }

    [Test]
    public void ReportsCollisionWithTwoExplicitNames()
    {
        string input = """
            public partial class SomeViewModel
            {
                [Notify("Prop")]
                private string _foo;
                [Notify("Prop")]
                private string _bar;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters)
            .HasDiagnostics(
            // (4,20): Warning INPC004: Member '_foo' will have the same generated property name 'Prop' as member '_bar'
            // _foo
            Diagnostic("INPC004", @"_foo").WithLocation(4, 20),

            // (6,20): Warning INPC004: Member '_bar' will have the same generated property name 'Prop' as member '_foo'
            // _bar
            Diagnostic("INPC004", @"_bar").WithLocation(6, 20)
        ));
    }

    [Test]
    public void ReportsCollisionWithExplicitNamesInBaseClass()
    {
        string input = """
            public partial class Base
            {
                [Notify("Prop")]
                private int _foo;
            }
            public partial class Derived : Base
            {
                [Notify("Prop")]
                private int _bar;
            }
            """;

        this.AssertThat(input, It
            .HasFile("Base", StandardRewriters)
            .HasFile("Derived", StandardRewriters)
            .HasDiagnostics(
                // (9,17): Warning INPC003: Attempted to generate property 'Prop' for member '_bar', but a member with that name already exists. Skipping this property
                // _bar
                Diagnostic("INPC003", @"_bar").WithLocation(9, 17)
        ));
    }
}
