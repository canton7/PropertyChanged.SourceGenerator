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
public partial class SomeViewModel
{
}";
            this.AssertSource(input);
        }
    }
}
