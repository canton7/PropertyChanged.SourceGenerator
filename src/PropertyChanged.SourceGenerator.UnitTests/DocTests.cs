using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests;

[TestFixture]
public class DocTests : TestsBase
{
    [Test]
    public void CopiesDocs()
    {
        string input = @"
public partial class SomeViewModel
{
    /// <summary>
    /// The Summary
    /// </summary>
    /// <description>
    /// The Description.
    ///     Indented line
    /// </description>
    [Notify] private string _foo;
}";
        string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    /// <summary>
    /// The Summary
    /// </summary>
    /// <description>
    /// The Description.
    ///     Indented line
    /// </description>
    public string Foo { get; set; }
}";

        this.AssertThat(input, It.HasFile("SomeViewModel", expected, StandardRewriters));
    }

    [Test]
    public void HandlesMalformedXml()
    {
        string input = @"
public partial class SomeViewModel
{
/// <summary>
/// Test
/// </summarry>
[Notify] private string _foo;
}";
        string expected = @"
partial class SomeViewModel : global::System.ComponentModel.INotifyPropertyChanged
{
    public string Foo { get; set; }
}";
        this.AssertThat(input, It.HasFile("SomeViewModel", expected, StandardRewriters));
    }
}
