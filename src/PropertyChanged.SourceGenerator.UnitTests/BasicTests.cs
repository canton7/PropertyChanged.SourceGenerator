using NUnit.Framework;
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
        public void Foo()
        {
            string input = @"
using PropertyChanged.SourceGenerator;
public partial class SomeViewModel
{
    [Notify]
    private string _foo;
}";
            this.AssertSource(input);
        }
    }
}
