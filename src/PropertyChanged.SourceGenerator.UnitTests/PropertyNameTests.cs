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

            this.AssertSource(expected, input, RemovePropertiesRewriter.Instance);
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

            this.AssertSource(expected, input, RemovePropertiesRewriter.Instance, diagnostics:
                // (5,20): Warning INPC003: Attempted to generate property 'Foo' for member '_foo', but a member with that name already exists. Skipping this property
                // _foo
                Diagnostic("INPC003", @"_foo").WithLocation(5, 20));
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

            this.AssertSource(expected, input, RemovePropertiesRewriter.Instance, diagnostics:
                // (5,20): Warning INPC003: Attempted to generate property 'Prop' for member '_foo', but a member with that name already exists. Skipping this property
                // _foo
                Diagnostic("INPC003", @"_foo").WithLocation(5, 20));
        }
    }
}
