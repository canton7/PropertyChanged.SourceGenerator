using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests;

public class AdditionalPropertyGenerationTests : TestsBase
{
    [Test]
    public void AddsVirtual()
    {
        string input = """
            public partial class SomeViewModel
            {
                [Notify(IsVirtual = true)]
                private int _foo;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", StandardRewriters));
    }
}
