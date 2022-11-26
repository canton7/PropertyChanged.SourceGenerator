using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests;

[TestFixture]
public class PropertyAttributeTests : TestsBase
{
    [Test]
    public void CopiesAttributeOntoGeneratedProperty()
    {
        string input = """
            public partial class SomeViewModel
            {
                [PropertyAttribute("[System.Xml.Serialization.XmlIgnore]")]
                [Notify] private string _foo;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters));
    }

    [Test]
    public void AddsLeadingAndTrailingBracketsIfOmitted()
    {
        string input = """
            public partial class SomeViewModel
            {
                [PropertyAttribute("System.Xml.Serialization.XmlIgnore")]
                [Notify] private string _foo;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters));
    }

    [Test]
    public void WarnsIfAttributePlacedOnMemberWithoutNotify()
    {
        string input = """
            public partial class SomeViewModel
            {
                [PropertyAttribute("System.Xml.Serialization.XmlIgnore")]
                private string _foo;
            }
            """;
        this.AssertThat(input, It.HasDiagnostics(
            // (3,6): Warning INPC008: [AlsoNotify] is only valid on members which also have [Notify]. Skipping
            // PropertyAttribute("System.Xml.Serialization.XmlIgnore")
            Diagnostic("INPC008", @"PropertyAttribute(""System.Xml.Serialization.XmlIgnore"")").WithLocation(3, 6)
        ));
    }
}
