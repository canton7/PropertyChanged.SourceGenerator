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
public class RaisePropertyChangingCallTests : TestsBase
{
    private static readonly CSharpSyntaxVisitor<SyntaxNode?>[] rewriters = new CSharpSyntaxVisitor<SyntaxNode?>[]
    {
        RemoveInpcMembersRewriter.Changed, RemovePropertiesRewriter.Instance, RemoveDocumentationRewriter.Instance,
    };

    [Test]
    public void DoesNotGenerateInpcInterfaceIfNotSpecified()
    {
        string input = """
            public partial class SomeViewModel
            {
                [Notify]
                private string _foo;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void DoesNotGenerateInpcInterfaceIfAlreadySpecified()
    {
        string input = """
            using System.ComponentModel;
            public partial class SomeViewModel : INotifyPropertyChanging
            {
                [Notify]
                private string _foo;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void FindsAndCallsMethodWithEventArgs()
    {
        string input = """
            using System.ComponentModel;
            public class Base : INotifyPropertyChanging
            {
                public event PropertyChangingEventHandler PropertyChanging;
                protected virtual void OnPropertyChanging(PropertyChangingEventArgs ea) =>
                    PropertyChanging?.Invoke(this, ea);
            }
            public partial class Derived : Base
            {
                [Notify]
                private string _foo;
            }
            """;

        this.AssertThat(input, It.HasFile("Derived", RemoveInpcMembersRewriter.Changed));
    }

    [Test]
    public void FindsAndCallsMethodWithStringName()
    {
        string input = """
            using System.ComponentModel;
            public partial class SomeViewModel : INotifyPropertyChanging
            {
                public event PropertyChangingEventHandler PropertyChanging;
                private void NotifyPropertyChanging(string name) =>
                    PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(name));
                [Notify]
                private string _foo;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.Changed));
    }

    [Test]
    public void FindsAndCallsMethodWithEventArgsAndOldValue()
    {
    string input = """
            using System.ComponentModel;
            public partial class SomeViewModel : INotifyPropertyChanging
            {
                public event PropertyChangingEventHandler PropertyChanging;
                private void NotifyPropertyChanging(PropertyChangingEventArgs args, object oldValue) =>
                    PropertyChanging?.Invoke(this, args);
                [Notify]
                private string _foo;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.Changed));
    }

    [Test]
    public void FindsAndCallsMethodWithStringNameAndOldValue()
    {
        string input = """
            using System.ComponentModel;
            public partial class SomeViewModel : INotifyPropertyChanging
            {
                public event PropertyChangingEventHandler PropertyChanging;
                private void NotifyPropertyChanging(string name, object oldValue) =>
                    PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(name));
                [Notify]
                private string _foo;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.Changed));
    }

    [Test]
    public void RaisesIfMethodFoundWithBadSignature()
    {
        string input = """
            using System.ComponentModel;
            public partial class SomeViewModel : INotifyPropertyChanging
            {
                public event PropertyChangingEventHandler PropertyChanging;
                internal void OnPropertyChanging(string name, string other) =>
                    PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(name));
                [Notify]
                private string _foo;
            }
            """;

        this.AssertThat(input, It.HasDiagnostics(
            // (2,22): Warning INPC029: Found one or more methods called 'OnPropertyChanging' to raise the PropertyChanging event, but they had an unrecognised signatures or were inaccessible. No PropertyChanging events will be raised from this type
            // SomeViewModel
            Diagnostic("INPC029", @"SomeViewModel").WithLocation(2, 22)
        ));
    }

    [Test]
    public void PrefersMethodEarlierInListWithBadSignatureToOneLaterInListWithGoodSignature()
    {
        string input = """
            using System.ComponentModel;
            public partial class SomeViewModel : INotifyPropertyChanging
            {
                public event PropertyChangingEventHandler PropertyChanging;
                internal void OnPropertyChanging(string name, string other) =>
                    PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(name));
                private void RaisePropertyChanging(string name) { }
                [Notify]
                private string _foo;
            }
            """;

        this.AssertThat(input, It.HasDiagnostics(
            // (2,22): Warning INPC029: Found one or more methods called 'OnPropertyChanging' to raise the PropertyChanging event, but they had an unrecognised signatures or were inaccessible. No PropertyChanging events will be raised from this type
            // SomeViewModel
            Diagnostic("INPC029", @"SomeViewModel").WithLocation(2, 22)
        ));
    }

    [Test]
    public void SearchesForMethodFromTopOfTypeHierarchy()
    {
        string input = """
            using System.ComponentModel;
            public class A : INotifyPropertyChanging
            {
                public event PropertyChangingEventHandler PropertyChanging;
                protected virtual void NotifyOfPropertyChanging(PropertyChangingEventArgs ea) => PropertyChanging?.Invoke(this, ea);
            }
            public partial class B : A
            {
                [Notify]
                private string _foo;
            }
            public partial class C : B
            {
                [Notify]
                private string _bar;
                protected void OnPropertyChanging(string name) { }
            }
            """;

        this.AssertThat(input, It.HasFile("B", RemoveInpcMembersRewriter.Changed).HasFile("C", RemoveInpcMembersRewriter.Changed));
    }

    [Test]
    public void TakesNameFromPropertyChangedIfPossible()
    {
        string input = """
            using System.ComponentModel;
            public partial class SomeViewModel : INotifyPropertyChanged, INotifyPropertyChanging
            {
                public event PropertyChangedEventHandler PropertyChanged;
                protected virtual void RaisePropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

                [Notify]
                private int _foo;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void RaisesIfUserDefinedOverrideFound()
    {
        string input = """
            using System.ComponentModel;
            public partial class Base : INotifyPropertyChanging
            {
                public event PropertyChangingEventHandler PropertyChanging;
                protected virtual void OnPropertyChanging(string name) => PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(name));
            }

            public partial class Derived : Base
            {
                [Notify] private string _bar;
                protected override void OnPropertyChanging(string name) { }
            }
            """;

        this.AssertThat(input, It.HasDiagnostics(
            // (11,29): Warning INPC032: Method 'OnPropertyChanging' must not be overridden. Functionality such as dependencies on base properties will not work. Define a method called 'OnAnyPropertyChanged' instead
            // OnPropertyChanging
            Diagnostic("INPC032", @"OnPropertyChanging").WithLocation(11, 29)));
    }

    [Test]
    public void FindsGenericBaseClasses()
    {
        // https://github.com/canton7/PropertyChanged.SourceGenerator/issues/3

        string input = """
            using System.ComponentModel;
            public partial class A<T> : INotifyPropertyChanging
            {
                [Notify]
                private string _foo;
            }
            public partial class B : A<string>
            {
                [Notify]
                private string _bar;
            }
            """;
        // It doesn't generate a new RaisePropertyChanging method
        this.AssertThat(input, It.HasFile("B", rewriters));
    }
}
