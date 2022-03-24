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
    public class OnAnyPropertyChangedTests : TestsBase
    {
        [Test]
        public void GeneratesParameterless()
        {
            string input = @"
public partial class SomeViewModel
{
    [Notify] string _foo;
    private void OnAnyPropertyChanged(string propertyName) { }
}";
            string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public string Foo { get; set; }
    protected virtual void OnPropertyChanged(global::System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        this.OnAnyPropertyChanged(eventArgs.PropertyName);
        this.PropertyChanged?.Invoke(this, eventArgs);
    }
}";

            this.AssertThat(input, It.HasFile("SomeViewModel", expected, RemovePropertiesRewriter.Instance));
        }
    }

    // TODO:
    //  - Bad signatures
    //  - Overridden method
    //  - Shadowed method
    //  - All combinations of having old/new and not
    //  - string vs PropertyChangedEventArgs
    //  - Can't generate RaisePropertyChanged method
}
