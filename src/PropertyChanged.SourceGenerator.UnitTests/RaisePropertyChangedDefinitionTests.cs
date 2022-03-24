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
    public class RaisePropertyChangedDefinitionTests : TestsBase
    {
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
    private void OnPropertyChanged(string name) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
        public void RaisesIfNonVirtualBaseMethodAndOverrideRequired()
        {
            string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Bar"")]
    private string _foo;
}";
            string expected = @"
partial class Derived
{
    public string Foo { get; set; }
}";

            this.AssertThat(input, It.HasFile("Derived", expected, RemovePropertiesRewriter.Instance).HasDiagnostics(
                // (6,20): Warning INPC022: Method 'OnPropertyChanged' is non-virtual. Functionality such as dependencies on base properties will not work. Please make this method virtual
                // OnPropertyChanged
                Diagnostic("INPC022", @"OnPropertyChanged").WithLocation(6, 20),

                // (10,14): Warning INPC023: [DependsOn("Bar")] specified, but this will not be raised because the method to raise PropertyChanged events 'OnPropertyChanged' cannot defined or overridden by the source generator
                // DependsOn("Bar")
                Diagnostic("INPC023", @"DependsOn(""Bar"")").WithLocation(10, 14)
            ));
        }

        [Test]
        public void DefinesVirtual()
        {
            string input = @"
public partial class Derived
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

            string expected = @"
partial class Derived : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public string Bar { get; set; }
    protected virtual void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        this.PropertyChanged?.Invoke(this, eventArgs);
        switch (eventArgs.PropertyName)
        {
            case @""Foo"":
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
                break;
        }
    }
}";

            this.AssertThat(input, It.HasFile("Derived", expected, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void DefinesOverrideStringNoOldAndNew()
        {
            string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string name) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

            string expected = @"
partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanged(string propertyName)
    {
        base.OnPropertyChanged(propertyName);
        switch (propertyName)
        {
            case @""Foo"":
                this.OnPropertyChanged(@""Bar"");
                break;
        }
    }
}";

            this.AssertThat(input, It.HasFile("Derived", expected, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void DefinesOverrideStringOldAndNew()
        {
            string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string name, object oldValue, object newValue) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

            string expected = @"
partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanged(string propertyName, object oldValue, object newValue)
    {
        base.OnPropertyChanged(propertyName, oldValue, newValue);
        switch (propertyName)
        {
            case @""Foo"":
                this.OnPropertyChanged(@""Bar"", (object)null, this.Bar);
                break;
        }
    }
}";

            this.AssertThat(input, It.HasFile("Derived", expected, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void DefinesOverrideEventArgsNoOldAndNew()
        {
            string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs args) => this.PropertyChanged?.Invoke(this, args);
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

            string expected = @"
partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        base.OnPropertyChanged(eventArgs);
        switch (eventArgs.PropertyName)
        {
            case @""Foo"":
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar);
                break;
        }
    }
}";

            this.AssertThat(input, It.HasFile("Derived", expected, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void DefinesOverrideEventArgsOldAndNew()
        {
            string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs args, object oldValue, object newValue) => this.PropertyChanged?.Invoke(this, args);
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
}";

            string expected = @"
partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs, object oldValue, object newValue)
    {
        base.OnPropertyChanged(eventArgs, oldValue, newValue);
        switch (eventArgs.PropertyName)
        {
            case @""Foo"":
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar, (object)null, this.Bar);
                break;
        }
    }
}";

            this.AssertThat(input, It.HasFile("Derived", expected, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void CallsOnPropertyNameChangedNoOldAndNew()
        {
            string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs args, object oldValue, object newValue) => this.PropertyChanged?.Invoke(this, args);
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
    private void OnBarChanged() { }
}";

            string expected = @"
partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs, object oldValue, object newValue)
    {
        base.OnPropertyChanged(eventArgs, oldValue, newValue);
        switch (eventArgs.PropertyName)
        {
            case @""Foo"":
                this.OnBarChanged();
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar, (object)null, this.Bar);
                break;
        }
    }
}";

            this.AssertThat(input, It.HasFile("Derived", expected, RemovePropertiesRewriter.Instance));
        }

        [Test]
        public void CallsOnPropertyNameChangedOldAndNew()
        {
            string input = @"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs args, object oldValue, object newValue) => this.PropertyChanged?.Invoke(this, args);
}
public partial class Derived : Base
{
    [Notify, DependsOn(""Foo"")] private string _bar;
    private void OnBarChanged(string oldValue, string newValue) { }
}";

            string expected = @"
partial class Derived
{
    public string Bar { get; set; }
    protected override void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs, object oldValue, object newValue)
    {
        base.OnPropertyChanged(eventArgs, oldValue, newValue);
        switch (eventArgs.PropertyName)
        {
            case @""Foo"":
                this.OnBarChanged(default(string), this.Bar);
                this.OnPropertyChanged(global::PropertyChanged.SourceGenerator.Internal.PropertyChangedEventArgsCache.Bar, (object)null, this.Bar);
                break;
        }
    }
}";

            this.AssertThat(input, It.HasFile("Derived", expected, RemovePropertiesRewriter.Instance));
        }

        [TestCase("public")]
        [TestCase("protected internal")]
        [TestCase("protected")]
        [TestCase("internal")]
        [TestCase("private protected")]
        public void CopiesBaseMethodAccessibility(string accessibility)
        {
            string input = @$"
using System.ComponentModel;
public class Base : INotifyPropertyChanged
{{
    public event PropertyChangedEventHandler PropertyChanged;
    {accessibility} virtual void OnPropertyChanged(string name) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}}
public partial class Derived : Base
{{
    [Notify, DependsOn(""Foo"")] private string _bar;
}}";

            string expected = @$"
partial class Derived
{{
    public string Bar {{ get; set; }}
    {accessibility} override void OnPropertyChanged(string propertyName)
    {{
        base.OnPropertyChanged(propertyName);
        switch (propertyName)
        {{
            case @""Foo"":
                this.OnPropertyChanged(@""Bar"");
                break;
        }}
    }}
}}";

            this.AssertThat(input, It.HasFile("Derived", expected, RemovePropertiesRewriter.Instance));
        }
    }
}
