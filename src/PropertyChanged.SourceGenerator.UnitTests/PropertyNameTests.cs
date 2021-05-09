using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests
{
    [TestFixture]
    public class PropertyNameTests : TestsBase
    {
        [Test]
        public void UsesExplicitNameIfGiven()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify(""Prop"")]
    private string _foo;
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public string Prop { get; set; }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void ReportsNameCollisionInferred()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public string Foo;
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance)
                .HasDiagnostics(
                // (5,20): Warning INPC003: Attempted to generate property 'Foo' for member '_foo', but a member with that name already exists. Skipping this property
                // _foo
                Diagnostic("INPC003", @"_foo").WithLocation(5, 20)
            ));
        }

        [Test]
        public void ReportsNameCollisionExplicit()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify(""Prop"")]
    private string _foo;
    public string Prop;
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance)
                .HasDiagnostics(
                // (5,20): Warning INPC003: Attempted to generate property 'Prop' for member '_foo', but a member with that name already exists. Skipping this property
                // _foo
                Diagnostic("INPC003", @"_foo").WithLocation(5, 20)
            ));
        }

        [Test]
        public void ReportsNameCollisionInBaseClass()
        {
            string input = @"
public partial class SomeViewModelBase
{
    public string Prop;
}
public partial class SomeViewModel : SomeViewModelBase
{
    [Notify(""Prop"")]
    private string _foo;
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance)
                .HasDiagnostics(
                // (9,20): Warning INPC003: Attempted to generate property 'Prop' for member '_foo', but a member with that name already exists. Skipping this property
                // _foo
                Diagnostic("INPC003", @"_foo").WithLocation(9, 20)
            ));
        }

        [Test]
        public void ReportsCollisionWithTwoExplicitNames()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify(""Prop"")]
    private string _foo;
    [Notify(""Prop"")]
    private string _bar;
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance)
                .HasDiagnostics(
                // (5,20): Warning INPC004: Member '_foo' will have the same generated property name 'Prop' as member '_bar'
                // _foo
                Diagnostic("INPC004", @"_foo").WithLocation(5, 20),

                // (7,20): Warning INPC004: Member '_bar' will have the same generated property name 'Prop' as member '_foo'
                // _bar
                Diagnostic("INPC004", @"_bar").WithLocation(7, 20)
            ));
        }

        [Test]
        public void ReportsCollisionWithExplicitNamesInBaseClass()
        {
            string input = @"
public partial class Base
{
    [Notify(""Prop"")]
    private int _foo;
}
public partial class Derived : Base
{
    [Notify(""Prop"")]
    private int _bar;
}";
            string expectedBase = @"
partial class Base : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public int Prop { get; set; }
}";
            string expectedDerived = @"
partial class Derived
{
}";

            this.AssertThat(input, It
                .HasFile("Base", expectedBase, RemovePropertiesRewriter.Instance)
                .HasFile("Derived", expectedDerived, RemovePropertiesRewriter.Instance)
                .HasDiagnostics(
                // (10,17): Warning INPC003: Attempted to generate property 'Prop' for member '_bar', but a member with that name already exists. Skipping this property
                // _bar
                Diagnostic("INPC003", @"_bar").WithLocation(10, 17)
            ));
        }
    }
}
