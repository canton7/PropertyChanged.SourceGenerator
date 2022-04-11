using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests;

[TestFixture]
public class AccessibilityTests : TestsBase
{
    [Test]
    public void HandlesGetterMoreAccessibleThanSetter()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify(Getter.Internal, Setter.Private)]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters));
    }

    [Test]
    public void HandlesGetterLessAccessibleThanSetter()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify(Getter.PrivateProtected, Setter.Protected)]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters));
    }

    [Test]
    public void HandlesGetterAndSetterEquallyAccessible()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify(Getter.Internal, Setter.Internal)]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters));
    }

    [Test]
    public void HandlesInternalGetterProtectedSetter()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify(Getter.Internal, Setter.Protected)]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters)
            .HasDiagnostics(
            // (4,6): Warning INPC005: C# propertes may not have an internal getter and protected setter, or protected setter and internal getter. Defaulting both to protected internal
            // Notify(Getter.Internal, Setter.Protected)
            Diagnostic("INPC005", @"Notify(Getter.Internal, Setter.Protected)").WithLocation(4, 6)
        ));
    }

    [Test]
    public void HandlesProtectedGetterInternalSetter()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify(Getter.Internal, Setter.Protected)]
    private string _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters)
            .HasDiagnostics(
            // (4,6): Warning INPC005: C# propertes may not have an internal getter and protected setter, or protected setter and internal getter. Defaulting both to protected internal
            // Notify(Getter.Internal, Setter.Protected)
            Diagnostic("INPC005", @"Notify(Getter.Internal, Setter.Protected)").WithLocation(4, 6)
        ));
    }

    [Test]
    public void RaisesIfBackingFieldIsReadOnly()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private readonly int _foo;
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters)
            .HasDiagnostics(
                // (5,26): Warning INPC018: Backing field '_foo' cannot be readonly. Skipping
                // _foo
                Diagnostic("INPC018", @"_foo").WithLocation(5, 26)
        ));
    }

    [Test]
    public void RaisesIfBackingPropertyIsGetterOnly()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private int _foo { get; }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters)
            .HasDiagnostics(
                // (5,17): Warning INPC019: Backing property '_foo' cannot be getter-only. Skipping
                // _foo
                Diagnostic("INPC019", @"_foo").WithLocation(5, 17)
        ));
    }

    [Test]
    public void RaisesIfBackingPropertyIsSetterOnly()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private int _foo { set { } }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters)
            .HasDiagnostics(
                // (5,17): Warning INPC019: Backing property '_foo' must have a getter and a setter. Skipping
                // _foo
                Diagnostic("INPC019", @"_foo").WithLocation(5, 17)
        ));
    }
}
