using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests;

[TestFixture]
public class DocTests : TestsBase
{
    private static readonly CSharpSyntaxVisitor<SyntaxNode?>[] rewriters = new CSharpSyntaxVisitor<SyntaxNode?>[]
    {
        RemovePropertiesRewriter.Instance, RemoveInpcMembersRewriter.All
    };

    [Test]
    public void CopiesDocs()
    {
        string input = """
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
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void HandlesMalformedXml()
    {
        string input = """
            public partial class SomeViewModel
            {
            /// <summary>
            /// Test
            /// </summarry>
            [Notify] private string _foo;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", rewriters));
    }

    [Test]
    public void GeneratesOnPropertyChangedOrChangingDocNoOldAndNew()
    {
        string input = """
            using System.ComponentModel;
            public partial class SomeViewModel : INotifyPropertyChanging
            {
                [Notify] private int _foo;
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", RemovePropertiesRewriter.Instance));
    }

    [Test]
    public void GeneratesOnPropertyChangedOrChangingDocOldAndNew()
    {
        string input = """
            using System.ComponentModel;
            public partial class SomeViewModel : INotifyPropertyChanging
            {
                [Notify] private int _foo;
                private void OnAnyPropertyChanged(string name, object oldValue, object newValue) { }
                private void OnAnyPropertyChanging(string name, object oldValue) { }
            }
            """;

        this.AssertThat(input, It.HasFile("SomeViewModel", RemovePropertiesRewriter.Instance));
    }
}
