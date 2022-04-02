using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests;

[TestFixture]
public class AlsoNotifyTests : TestsBase
{
    [Test]
    public void RaisesIfAlsoNotifyAppliedToMemberWithoutNotifyAttribute()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify]
    private string _foo;

    [AlsoNotify(""Foo"")]
    private string _bar;
}";
        this.AssertThat(input, It.HasDiagnostics(
            // (7,6): Warning INPC008: [AlsoNotify] is only valid on members which also have [Notify]. Skipping
            // AlsoNotify("Foo")
            Diagnostic("INPC008", @"AlsoNotify(""Foo"")").WithLocation(7, 6)
        ));
    }

    [Test]
    public void RaisesIfBackingMemberNameUsed()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify, AlsoNotify(nameof(_bar))]
    private string _foo;

    [Notify]
    private string _bar;
}";

        this.AssertThat(input, It.HasDiagnostics(
            // (4,14): Warning INPC009: Unable to find a property called '_bar' on this type or its base types. This event will still be raised
            // AlsoNotify(nameof(_bar))
            Diagnostic("INPC009", @"AlsoNotify(nameof(_bar))").WithLocation(4, 14)
        ));
    }

    [Test]
    public void RaisesIfPropertyDefinedOnChildClass()
    {
        string input = @"
public partial class Base
{
    [Notify, AlsoNotify(""Bar"")]
    private string _foo;
}
public partial class Derived : Base
{
    [Notify]
    private string _bar;
}";
        string expected = @"
partial class Base : global::System.ComponentModel.INotifyPropertyChanged
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
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
            }
        }
    }
}";

        this.AssertThat(input, It.HasFile("Base", expected, RemoveInpcMembersRewriter.Instance)
            .HasDiagnostics(
            // (4,14): Warning INPC009: Unable to find a property called 'Bar' on this type or its base types. This event will still be raised
            // AlsoNotify("Bar")
            Diagnostic("INPC009", @"AlsoNotify(""Bar"")").WithLocation(4, 14)
        ));
    }

    [Test]
    public void NotifiesGeneratedProperty()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify, AlsoNotify(""Bar"")]
    private string _foo;

    [Notify]
    private string _bar;
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
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
            }
        }
    }
    public string Bar
    {
        get => this._bar;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._bar))
            {
                this._bar = value;
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
            }
        }
    }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance));
    }

    [Test]
    public void NotifiesPreExistingProperty()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify, AlsoNotify(nameof(Bar))]
    private string _foo;

    public string Bar { get; set; }
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
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
            }
        }
    }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance));
    }

    [Test]
    public void NotifiesGeneratedPropertyOnBaseClass()
    {
        string input = @"
public partial class Base
{
    [Notify]
    private string _foo;
}
public partial class Derived : Base
{
    [Notify, AlsoNotify(""Foo"")]
    private string _bar;
}";
        string expected = @"
partial class Derived
{
    public string Bar
    {
        get => this._bar;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._bar))
            {
                this._bar = value;
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
            }
        }
    }
}";

        this.AssertThat(input, It.HasFile("Derived", expected, RemoveInpcMembersRewriter.Instance));
    }

    [Test]
    public void NotifiesPreExistingPropertyOnBaseClass()
    {
        string input = @"
public class Base
{
    public string Foo { get; set; }
}
public partial class Derived : Base
{
    [Notify, AlsoNotify(nameof(Foo))]
    private string _bar;
}";
        string expected = @"
partial class Derived : global::System.ComponentModel.INotifyPropertyChanged
{
    public string Bar
    {
        get => this._bar;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._bar))
            {
                this._bar = value;
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
            }
        }
    }
}";

        this.AssertThat(input, It.HasFile("Derived", expected, RemoveInpcMembersRewriter.Instance));
    }

    [Test]
    public void HandlesStandardNonPropertyNames()
    {
        string input = @"
public partial class SomeViewModel
{
    public string this[int index] => """";

    [Notify, AlsoNotify(null, """", ""Item[]"")]
    private string _foo;
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
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Null);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Empty);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Item__);
            }
        }
    }
}";
        string expectedCache = @"
namespace PropertyChanged.SourceGenerator.Internal
{
    internal static class PropertyChangedEventArgsCache
    {
        private static global::System.ComponentModel.PropertyChangedEventArgs _Empty;
        public static global::System.ComponentModel.PropertyChangedEventArgs Empty => _Empty ??= new global::System.ComponentModel.PropertyChangedEventArgs(@"""");
        private static global::System.ComponentModel.PropertyChangedEventArgs _Foo;
        public static global::System.ComponentModel.PropertyChangedEventArgs Foo => _Foo ??= new global::System.ComponentModel.PropertyChangedEventArgs(@""Foo"");
        private static global::System.ComponentModel.PropertyChangedEventArgs _Item__;
        public static global::System.ComponentModel.PropertyChangedEventArgs Item__ => _Item__ ??= new global::System.ComponentModel.PropertyChangedEventArgs(@""Item[]"");
        private static global::System.ComponentModel.PropertyChangedEventArgs _Null;
        public static global::System.ComponentModel.PropertyChangedEventArgs Null => _Null ??= new global::System.ComponentModel.PropertyChangedEventArgs(null);
    }
}";

        this.AssertThat(input, It
            .HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance)
            .HasFile("PropertyChangedEventArgsCache", expectedCache));
    }

    [Test]
    public void RaisesIfAutoNotifyingSelf()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify, AlsoNotify(""Foo"")]
    private string _foo;
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
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
            }
        }
    }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance).HasDiagnostics(
            // (4,14): Warning INPC012: Property 'Foo' cannot have an [AlsoNotify] attribute which refers to that same property
            // AlsoNotify("Foo")
            Diagnostic("INPC012", @"AlsoNotify(""Foo"")").WithLocation(4, 14)));
    }

    [Test]
    public void PassesOldAndNewValue()
    {
        string input = @"
using System.ComponentModel;
using System.Collections.Generic;
public partial class SomeViewModel
{
    [Notify, AlsoNotify(""Bar"")]
    private string _foo;

    public List<SomeViewModel> Bar { get; set; }

    public void OnPropertyChanged(string propertyName, object oldValue, object newValue) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
                global::System.Collections.Generic.List<global::SomeViewModel> old_Bar = this.Bar;
                this._foo = value;
                this.OnPropertyChanged(@""Foo"", old_Foo, this.Foo);
                this.OnPropertyChanged(@""Bar"", old_Bar, this.Bar);
            }
        }
    }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance));
    }

    [Test]
    public void PassesNullForOldAndNewValueIfPropertyDoesNotExist()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify, AlsoNotify(""Item[]"", ""NonExistent"", """")]
    private string _foo;

    public void OnPropertyChanged(string propertyName, object oldValue, object newValue) { }
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
                this.OnPropertyChanged(@""Foo"", old_Foo, this.Foo);
                this.OnPropertyChanged(@"""", (object)null, (object)null);
                this.OnPropertyChanged(@""Item[]"", (object)null, (object)null);
                this.OnPropertyChanged(@""NonExistent"", (object)null, (object)null);
            }
        }
    }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance)
            .HasDiagnostics(
                // (4,14): Warning INPC009: Unable to find a property called 'Item[]' on this type or its base types. This event will still be raised
                // AlsoNotify("Item[]", "NonExistent", "")
                Diagnostic("INPC009", @"AlsoNotify(""Item[]"", ""NonExistent"", """")").WithLocation(4, 14),

                // (4,14): Warning INPC009: Unable to find a property called 'NonExistent' on this type or its base types. This event will still be raised
                // AlsoNotify("Item[]", "NonExistent", "")
                Diagnostic("INPC009", @"AlsoNotify(""Item[]"", ""NonExistent"", """")").WithLocation(4, 14)
                ));
    }

    [Test]
    public void HandlesPathologicalAttributeCases()
    {
        string input = @"
public partial class SomeViewModel
{
    [Notify, AlsoNotify, AlsoNotify(new string[0]), AlsoNotify(new[] { null, """" }), AlsoNotify(nameof(NonExistent))]
    private string _foo;
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
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Foo);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Null);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Empty);
            }
        }
    }
}";
        this.AssertThat(input, It
            .HasFile("SomeViewModel", expected, RemoveInpcMembersRewriter.Instance)
            .AllowCompilationDiagnostics("CS0103")); // Unknown member NonExistent
    }
}
