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
    public class BasicTests : TestsBase
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
    public string Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._foo))
            {
                this._foo = value;
                this.PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(""Foo""));
            }
        }
    }
}";

            this.AssertSource(expected, input);
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
    public string Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._foo))
            {
                this._foo = value;
                this.PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(""Foo""));
            }
        }
    }
}";

            this.AssertSource(expected, input);
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
    public string Foo
    {
        get => this._foo;
        set
        {
            if (!global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(value, this._foo))
            {
                this._foo = value;
                this.PropertyChanged?.Invoke(this, new global::System.ComponentModel.PropertyChangedEventArgs(""Foo""));
            }
        }
    }
}";

            this.AssertSource(expected, input);
        }
    }
}
