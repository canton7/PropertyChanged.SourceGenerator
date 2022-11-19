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
public class OnAnyPropertyChangingTests : TestsBase
{
    private static readonly CSharpSyntaxVisitor<SyntaxNode?>[] rewriters = new CSharpSyntaxVisitor<SyntaxNode?>[]
    {
        RemoveInpcMembersRewriter.Changed, RemovePropertiesRewriter.Instance, RemoveDocumentationRewriter.Instance
    };

    [Test]
    public void GeneratesParameterlessWhenAlsoGeneratingRaisePropertyChanging()
    {
        string input = """
            using System.ComponentModel;
            public partial class SomeViewModel : INotifyPropertyChanging
            {
                [Notify] string _foo;
                private void OnAnyPropertyChanging(string propertyName) { }
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void GeneratesOldWhenAlsoGeneratingRaisePropertyChanging()
    {
        string input = """
            using System.ComponentModel;
            public partial class SomeViewModel : INotifyPropertyChanging
            {
                [Notify] string _foo;
                private void OnAnyPropertyChanging(string propertyName, object oldValue) { }
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void GeneratesParameterlessWhenOverridingParameterless()
    {
        string input = """
            using System.ComponentModel;
            public partial class Base : INotifyPropertyChanging
            {
                public event PropertyChangingEventHandler PropertyChanging;
                protected virtual void OnPropertyChanging(string propertyName) => PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
            }
            public partial class Derived : Base
            {
                [Notify] private int _foo;
                private void OnAnyPropertyChanging(string name) { }
            }
            """;

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void GeneratesParameterlessWhenOverridingOld()
    {
        string input = """
            using System.ComponentModel;
            public partial class Base : INotifyPropertyChanging
            {
                public event PropertyChangingEventHandler PropertyChanging;
                protected virtual void OnPropertyChanging(string propertyName, object o) => PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
            }
            public partial class Derived : Base
            {
                [Notify] private int _foo;
                private void OnAnyPropertyChanging(string name) { }
            }
            """;

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void GeneratesOldWhenOverridingParameterless()
    {
        string input = """
            using System.ComponentModel;
            public partial class Base : INotifyPropertyChanging
            {
                public event PropertyChangingEventHandler PropertyChanging;
                protected virtual void OnPropertyChanging(PropertyChangingEventArgs ea) => PropertyChanging?.Invoke(this, ea);
            }
            public partial class Derived : Base
            {
                [Notify] private int _foo;
                private void OnAnyPropertyChanging(string name, object o) { }
            }
            """;

        this.AssertThat(input, It.HasFile("Derived", rewriters).HasDiagnostics(
            // (10,18): Warning INPC034: The OnAnyPropertyChanging method has an 'oldValue' parameter, but the 'OnPropertyChanging' method defined in a base class does not. Please add this parameter to 'OnPropertyChanging'
            // OnAnyPropertyChanging
            Diagnostic("INPC034", @"OnAnyPropertyChanging").WithLocation(10, 18)
        ));
    }

    [Test]
    public void GeneratesOldWhenOverridingOld()
    {
        string input = """
            using System.ComponentModel;
            public partial class Base : INotifyPropertyChanging
            {
                public event PropertyChangingEventHandler PropertyChanging;
                protected virtual void OnPropertyChanging(PropertyChangingEventArgs ea, object o) => PropertyChanging?.Invoke(this, ea);
            }
            public partial class Derived : Base
            {
                [Notify] private int _foo;
                private void OnAnyPropertyChanging(string name, object o) { }
            }
            """;

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void GeneratesForShadowedMethod()
    {
        string input = """
            using System.ComponentModel;
            public partial class Base : INotifyPropertyChanging
            {
                [Notify] private string _foo;
                protected void OnAnyPropertyChanging(string propertyName) { }
            }
            public partial class Derived : Base
            {
                [Notify] private string _bar;
                protected new void OnAnyPropertyChanging(string propertyName) { }
            }
            """;

        this.AssertThat(input, It.HasFile("Derived", rewriters));
    }

    [Test]
    public void DoesNotGenerateForOverriddenMethod()
    {
        string input = """
            using System.ComponentModel;
            public partial class Base : INotifyPropertyChanging
            {
                [Notify] private string _foo;
                protected virtual void OnAnyPropertyChanging(string propertyName) { }
            }
            public partial class Derived : Base
            {
                [Notify] private string _bar;
                protected override void OnAnyPropertyChanging(string propertyName) { }
            }
            """;

        this.AssertThat(input, It.HasFile("Base", rewriters)
            .HasFile("Derived", rewriters));
    }

    [Test]
    public void RaisesIfSignatureNotRecognise()
    {
        string input = """
            using System.ComponentModel;
            public partial class SomeViewModel : INotifyPropertyChanging
            {
                [Notify] private int _foo;
                private void OnAnyPropertyChanging() { }
            }
            """;

        this.AssertThat(input, It.HasDiagnostics(
            // (5,18): Warning INPC024: Found one or more OnAnyPropertyChanged methods, but none had the correct signature, or were inaccessible. Skipping
            // OnAnyPropertyChanging
            Diagnostic("INPC024", @"OnAnyPropertyChanging").WithLocation(5, 18)));
    }

    [Test]
    public void RaisesIfCannotGenerateRaisePropertyChangingMethod()
    {
        string input = """
            using System.ComponentModel;
            public partial class Base : INotifyPropertyChanging
            {
                public event PropertyChangingEventHandler PropertyChanging;
                protected void OnPropertyChanging(string name) => PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(name));
            }
            public partial class Derived : Base
            {
                [Notify] private int _foo;
                private void OnAnyPropertyChanging(string name) { }
            }
            """;

        this.AssertThat(input, It.HasDiagnostics(
            // (5,20): Warning INPC031: Method 'OnPropertyChanging' is non-virtual. Functionality such as dependencies on base properties will not work. Please make this method virtual
            // OnPropertyChanging
            Diagnostic("INPC031", @"OnPropertyChanging").WithLocation(5, 20),

            // (10,18): Warning INPC033: OnAnyPropertyChanging method will not be called because the method to raise PropertyChanging events 'OnPropertyChanging' cannot defined or overridden by the source generator
            // OnAnyPropertyChanging
            Diagnostic("INPC033", @"OnAnyPropertyChanging").WithLocation(10, 18)
        ));
    }
}
