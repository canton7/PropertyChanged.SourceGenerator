using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using PropertyChanged.SourceGenerator.UnitTests.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests;

[TestFixture]
public class NullableTypeTests : TestsBase
{
    private static readonly CSharpSyntaxVisitor<SyntaxNode?>[] rewriters = new CSharpSyntaxVisitor<SyntaxNode?>[]
    {
        RemovePropertiesRewriter.Instance, RemoveDocumentationRewriter.Instance,
    };

    [Test]
    public void GeneratesNullableEventIfInCompilationNullableContext()
    {
        string input = """
            public partial class SomeViewModel
            {
                [Notify]
                private string _foo = "";
            }
            """;

        this.AssertThat(
            input,
            It.HasFile("SomeViewModel", rewriters),
            nullableContextOptions: NullableContextOptions.Enable);
    }

    [Test]
    public void DoesNotGenerateNullableEventIfInFileNullableContext()
    {
        string input = """
            #nullable enable
            public partial class SomeViewModel
            {
                [Notify]
                private string _foo = "";
            }
            """;

        this.AssertThat(
            input,
            It.HasFile("SomeViewModel", rewriters),
            nullableContextOptions: NullableContextOptions.Disable);
        ;
    }

    [Test]
    public void GeneratesNullablePropertiesIfInCompilationNullableContext()
    {
        string input = """
            public partial class SomeViewModel
            {
                [Notify]
                private string? _nullable;
                [Notify]
                private string _notNullable = "";
            #nullable disable
                [Notify]
                private string _oblivious;
            #nullable restore
                [Notify]
                private int? _nullableValue;
            }
            """;

        this.AssertThat(
            input,
            It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.All),
            nullableContextOptions: NullableContextOptions.Enable);
    }

    [Test]
    public void GeneratesNullablePropertiesIfInFilenNullableContext()
    {
        string input = """
            #nullable enable
            public partial class SomeViewModel
            {
                [Notify]
                private string? _nullable;
                [Notify]
                private string _notNullable = "";
            #nullable disable
                [Notify]
                private string _oblivious;
            #nullable restore
                [Notify]
                private int? _nullableValue;
            }
            """;

        this.AssertThat(
            input,
            It.HasFile("SomeViewModel", RemoveInpcMembersRewriter.All),
            nullableContextOptions: NullableContextOptions.Disable);
    }
}
