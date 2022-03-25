using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace PropertyChanged.SourceGenerator.UnitTests.Framework;

public class RemovePropertiesRewriter : CSharpSyntaxRewriter
{
    public static RemovePropertiesRewriter Instance { get; } = new();

    public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        // Remove the whitespace around the leading { and trailing }
        return base.VisitPropertyDeclaration(node
            .WithIdentifier(
                node.Identifier.WithTrailingTrivia(Whitespace(" ")))
            .WithAccessorList(
                node.AccessorList!
                    .WithOpenBraceToken(node.AccessorList.OpenBraceToken.WithoutTrivia())
                    .WithCloseBraceToken(node.AccessorList.CloseBraceToken
                        .WithLeadingTrivia(Whitespace(" "))
                        .WithTrailingTrivia(LineFeed))));
    }

    public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
    {
        return base.VisitAccessorDeclaration(node
            .WithBody(null)
            .WithExpressionBody(null)
            .WithKeyword(node.Keyword.WithTrailingTrivia(null))
            .WithLeadingTrivia(Whitespace(" "))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
    }
}
