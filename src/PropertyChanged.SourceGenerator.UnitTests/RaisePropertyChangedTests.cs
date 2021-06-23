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
    public class RaisePropertyChangedTests : TestsBase
    {
        [Test]
        public void GeneratesInpcInterfaceIfNotSpecified()
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
    public string Foo { get; set; }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, StandardRewriters));
        }

        [Test]
        public void DoesNotGenerateInpcInterfaceIfAlreadySpecified()
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
    public string Foo { get; set; }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, StandardRewriters));
        }

        [Test]
        public void GeneratesEventAndRaisePropertyChangedIfNotDefined()
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
    protected virtual void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        this.PropertyChanged?.Invoke(this, eventArgs);
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void GeneratesRaisePropertyChangedIfNotDefined()
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
    protected virtual void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        this.PropertyChanged?.Invoke(this, eventArgs);
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void RaisesIfEventButNoRaiseMethodOnBaseClass()
        {
            string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
}
public partial class Derived : Base
{
    [Notify]
    private string _foo;
}";
            
            this.AssertThat(input, It.HasDiagnostics(
                // (7,22): Warning INPC007: Could not find any suitable methods to raise the PropertyChanged event defined on a base class
                // Derived
                Diagnostic("INPC007", @"Derived").WithLocation(7, 22)
            ));
        }

        [Test]
        public void RaisesIfMethodOnBaseClassIsPrivate()
        {
            string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name) { }
}
public partial class Derived : Base
{
    [Notify]
    private string _foo;
}";

            this.AssertThat(input, It.HasDiagnostics(
                // (8,22): Warning INPC006: Found one or more methods called 'RaisePropertyChanged' to raise the PropertyChanged event, but they had an unrecognised signatures or were inaccessible
                // Derived
                Diagnostic("INPC006", @"Derived").WithLocation(8, 22)
            ));
        }

        [Test]
        public void FindsAndCallsMethodWithEventArgs()
        {
            string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs ea) =>
        PropertyChanged?.Invoke(this, ea);
}
public partial class Derived : Base
{
    [Notify]
    private string _foo;
}";
            string expected = @"
partial class Derived
{
    public string Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._foo))
            {
                this._foo = value;
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("Derived", expected));
        }

        [Test]
        public void FindsAndCallsMethodWithStringName()
        {
            string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    private void NotifyPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    [Notify]
    private string _foo;
}";
            string expected = @"
partial class SomeViewModel
{
    public string Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._foo))
            {
                this._foo = value;
                this.NotifyPropertyChanged(@""Foo"");
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected));
        }

        [Test]
        public void FindsAndCallsMethodWithEventArgsAndOldAndNewValues()
        {
            string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    private void NotifyPropertyChanged(PropertyChangedEventArgs args, object oldValue, object newValue) =>
        PropertyChanged?.Invoke(this, args);
    [Notify]
    private string _foo;
}";
            string expected = @"
partial class SomeViewModel
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
                this.NotifyPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo, old_Foo, this.Foo);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected));
        }

        [Test]
        public void FindsAndCallsMethodWithStringNameAndOldAndNewValues()
        {
            string input = @"
using System.ComponentModel;
public partial class SomeViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    private void NotifyPropertyChanged(string name, object oldValue, object newValue) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    [Notify]
    private string _foo;
}";
            string expected = @"
partial class SomeViewModel
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
                this.NotifyPropertyChanged(@""Foo"", old_Foo, this.Foo);
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected));
        }

        [Test]
        public void RaisesIfMethodFoundWithBadSignature()
        {
            string input = @"
using System.ComponentModel;
public partial class SomeViewModel
{
    public event PropertyChangedEventHandler PropertyChanged;
    internal void OnPropertyChanged(string name, string other) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    [Notify]
    private string _foo;
}";

            this.AssertThat(input, It.HasDiagnostics(
                // (3,22): Warning INPC006: Found one or more methods called 'RaisePropertyChanged' to raise the PropertyChanged event, but they had an unrecognised signatures or were inaccessible
                // SomeViewModel
                Diagnostic("INPC006", @"SomeViewModel").WithLocation(3, 22)
            ));
        }

        [Test]
        public void PrefersMethodEarlierInListWithBadSignatureToOneLaterInListWithGoodSignature()
        {
            string input = @"
using System.ComponentModel;
public partial class SomeViewModel
{
    public event PropertyChangedEventHandler PropertyChanged;
    internal void OnPropertyChanged(string name, string other) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private void RaisePropertyChanged(string name) { }
    [Notify]
    private string _foo;
}";

            this.AssertThat(input, It.HasDiagnostics(
                // (3,22): Warning INPC006: Found one or more methods called 'OnPropertyChanged' to raise the PropertyChanged event, but they had an unrecognised signatures or were inaccessible
                // SomeViewModel
                Diagnostic("INPC006", @"SomeViewModel").WithLocation(3, 22)
            ));
        }

        [Test]
        public void SearchesForMethodFromTopOfTypeHierarchy()
        {
            string input = @"
using System.ComponentModel;
public class A : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected void NotifyOfPropertyChange(PropertyChangedEventArgs ea) => PropertyChanged?.Invoke(this, ea);
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
    protected void OnPropertyChanged(string name) { }
}";
            string expectedB = @"
partial class B
{
    public string Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._foo))
            {
                this._foo = value;
                this.NotifyOfPropertyChange(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
            }
        }
    }
}";
            string expectedC = @"
partial class C
{
    public string Bar
    {
        get => this._bar;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._bar))
            {
                this._bar = value;
                this.OnPropertyChanged(@""Bar"");
            }
        }
    }
}";

            this.AssertThat(input, It.HasFile("B", expectedB).HasFile("C", expectedC));
        }
    }
}
