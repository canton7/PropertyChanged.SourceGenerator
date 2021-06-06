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
    public class OnPropertyNameChangedTests : TestsBase
    {
        [Test]
        public void GenerateParameterlessRaise()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public void OnFooChanged() { }
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public string Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._foo))
            {
                this._foo = value;
                this.OnFooChanged();
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance));
        }

        [Test]
        public void GeneratesOldAndNewWithMatchingDataType()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public void OnFooChanged(string oldValue, string newValue) { }
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public string Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._foo))
            {
                string old_Foo = this.Foo;
                this._foo = value;
                this.OnFooChanged(old_Foo, this.Foo);
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance));
        }

        [Test]
        public void GeneratesOldAndNewWithParentDataType()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public void OnFooChanged(object oldValue, object newValue) { }
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public string Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._foo))
            {
                string old_Foo = this.Foo;
                this._foo = value;
                this.OnFooChanged(old_Foo, this.Foo);
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance));
        }

        [Test]
        public void DoesNotMatchMethodWithDifferingParameterTypes()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
    public void OnFooChanged(object oldValue, string newValue) { }
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public string Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._foo))
            {
                this._foo = value;
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance)
                .HasDiagnostics(
                    // (6,17): Warning INPC013: Found one or more On{PropertyName}Changed methods called 'OnFooChanged' for property 'Foo', but none had the correct signature, or were inaccessible. Skipping
                    // OnFooChanged
                    Diagnostic("INPC013", @"OnFooChanged").WithLocation(6, 17)
                ));
        }

        [Test]
        public void GeneratesAlsoNotifyCallableParameterless()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
    public int Bar { get; }
    private void OnBarChanged() { }
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public int Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(value, this._foo))
            {
                this._foo = value;
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
                this.OnBarChanged();
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance));
        }

        [Test]
        public void GeneratesAlsoNotifyCallableParameters()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
    public int Bar { get; }
    private void OnBarChanged(int oldValue, int newValue) { }
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public int Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(value, this._foo))
            {
                int old_Bar = this.Bar;
                this._foo = value;
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
                this.OnBarChanged(old_Bar, this.Bar);
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance));
        }
        
        [Test]
        public void GeneratesAlsoNotifyOnBaseClass()
        {
            string input = @"
public partial class Base
{
    public int Bar { get; }
    protected void OnBarChanged(int oldValue, int newValue) { }
}
public partial class Derived : Base
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
}";
            string expected = @"
partial class Derived : global::System.ComponentModel.INotifyPropertyChanged
{
    public int Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(value, this._foo))
            {
                int old_Bar = this.Bar;
                this._foo = value;
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
                this.OnBarChanged(old_Bar, this.Bar);
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("Derived", expected, RemoveInpcMembersRewriter.Instance));
        }

        [Test]
        public void DoesNotGenerateAlsoNotifyWithPropertyOnBaseClassAndMethodOnDerived()
        {
            string input = @"
public partial class Base
{
    public int Bar { get; }
}
public partial class Derived : Base
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
    private void OnBarChanged(int oldValue, int newValue) { }
}";
            string expected = @"
partial class Derived : global::System.ComponentModel.INotifyPropertyChanged
{
    public int Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(value, this._foo))
            {
                int old_Bar = this.Bar;
                this._foo = value;
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
                this.OnBarChanged(old_Bar, this.Bar);
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("Derived", expected, RemoveInpcMembersRewriter.Instance));
        }

        [Test]
        public void DoesNotCallInaccessibleAlsoNotifyOnBaseClass()
        {
            string input = @"
public partial class Base
{
    public int Bar { get; }
    private void OnBarChanged(int oldValue, int newValue) { }
}
public partial class Derived : Base
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
}";
            string expected = @"
partial class Derived : global::System.ComponentModel.INotifyPropertyChanged
{
    public int Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(value, this._foo))
            {
                int old_Bar = this.Bar;
                this._foo = value;
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
                this.OnBarChanged(old_Bar, this.Bar);
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("Derived", expected, RemoveInpcMembersRewriter.Instance));
        }

        [Test]
        public void DoesNotGenerateAlsoNotifyNonCallable()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify, AlsoNotify(""Bar"")]
    private int _foo;
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public int Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(value, this._foo))
            {
                this._foo = value;
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance)
                .HasDiagnostics(
                    // (4,14): Warning INPC009: Unable to find a property called 'Bar' on this type or its base types. This event will still be raised
                    // AlsoNotify("Bar")
                    Diagnostic("INPC009", @"AlsoNotify(""Bar"")").WithLocation(4, 14)
                ));
        }

        [Test]
        public void GeneratesDependsOn()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify]
    private int _foo;
    [DependsOn(""Foo"")]
    public int Bar { get; }
    private void OnBarChanged(int oldValue, int newValue) { }
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public int Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(value, this._foo))
            {
                int old_Bar = this.Bar;
                this._foo = value;
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
                this.OnBarChanged(old_Bar, this.Bar);
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance));
        }

        [Test]
        public void GeneratesAutoDependsOn()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify]
    private int _foo;
    public int Bar => this.Foo + 2;
    private void OnBarChanged(int oldValue, int newValue) { }
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public int Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(value, this._foo))
            {
                int old_Bar = this.Bar;
                this._foo = value;
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
                this.OnBarChanged(old_Bar, this.Bar);
                this.RaisePropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance));
        }
    }
}
