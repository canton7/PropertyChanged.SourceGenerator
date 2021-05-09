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
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    internal string Foo { get; private set; }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance));
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
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    protected string Foo { private protected get; set; }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance));
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
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    internal string Foo { get; set; }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance));
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
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    protected internal string Foo { get; set; }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance)
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
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    protected internal string Foo { get; set; }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance)
                .HasDiagnostics(
                // (4,6): Warning INPC005: C# propertes may not have an internal getter and protected setter, or protected setter and internal getter. Defaulting both to protected internal
                // Notify(Getter.Internal, Setter.Protected)
                Diagnostic("INPC005", @"Notify(Getter.Internal, Setter.Protected)").WithLocation(4, 6)
            ));
        }
    }
}
