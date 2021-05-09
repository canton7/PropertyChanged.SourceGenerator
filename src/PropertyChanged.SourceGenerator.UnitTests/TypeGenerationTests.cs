using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertyChanged.SourceGenerator.UnitTests
{
    [TestFixture]
    public class TypeGenerationTests : TestsBase
    {
        [Test]
        public void GeneratesInpcInterfaceAndEventIfNotSpecified()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public string Foo { get; set; }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void GeneratesEventIfNotSpecified()
        {
            string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanged
{
    [Notify]
    private string _foo;
}";
            string expected = @"
partial class SomeViewModel
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public string Foo { get; set; }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void DoesNotGenerateEventIfAlreadySpecified()
        {
            string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    [Notify]
    private string _foo;
}";
            string expected = @"
partial class SomeViewModel
{
    public string Foo { get; set; }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void DoesNotGenerateEventOnDerivedClass()
        {
            string input = @"
public partial class Base
{
    [Notify]
    private string _foo;
}
public partial class Derived : Base
{
    [Notify]
    private string _bar;
}";
            string expectedBase = @"
partial class Base : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public string Foo { get; set; }
}";
            string expectedDerived = @"
partial class Derived
{
    public string Bar { get; set; }
}";

            this.AssertThat(input, It
                .HasFile("Base", expectedBase, RemovePropertiesRewriter.Instance)
                .HasFile("Derived", expectedDerived, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void GeneratesNamespace()
        {
            string input = @"
using System.ComponentModel;
namespace Test.Foo
{
    public partial class SomeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        [Notify]
        private string _foo;
    }
}";
            string expected = @"
namespace Test.Foo
{
    partial class SomeViewModel
    {
        public string Foo { get; set; }
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void RaisesIfTypeIsNotPartial()
        {
            string input = @"
using System.ComponentModel;
public class SomeViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    [Notify]
    private string _foo;
}";

            this.AssertThat(input, It.HasDiagnostics(
                // (3,14): Warning INPC002: Type 'SomeViewModel' must be partial in order for PropertyChanged.SourceGenerator to generate properties
                // SomeViewModel
                Diagnostic("INPC002", @"SomeViewModel").WithLocation(3, 14)
            ));
        }

        [Test]
        public void HandlesBadlyNamedGenericTypes()
        {
            string input = @"
public partial class SomeViewModel<@class>
{
    [Notify]
    private string _foo;
}";
            string expected = @"
partial class SomeViewModel<@class> : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public string Foo { get; set; }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void HandlesGenericTypesWithConstraints()
        {
            string input = @"
public partial class SomeViewModel<T> where T : class
{
    [Notify]
    private string _foo;
}";
            string expected = @"
partial class SomeViewModel<T> : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public string Foo { get; set; }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance));
        }
    }
}
