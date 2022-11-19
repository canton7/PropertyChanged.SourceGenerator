using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PropertyChanged.SourceGenerator.UnitTests.Framework;

public class RemoveDocumentationRewriter : CSharpSyntaxRewriter
{
    public static RemoveDocumentationRewriter Instance { get; } = new();

    public override SyntaxNode? Visit(SyntaxNode? node) => base.Visit(RemoveDocComments(node));

    private static SyntaxNode? RemoveDocComments(SyntaxNode? node)
    {
        if (node == null)
        {
            return node;
        }

        // Need to remove the SingleLineDocumentationCommentTrivia, and the WhitespaceTrivia just before it

        var trivia = node.GetLeadingTrivia();
        return node.WithLeadingTrivia(Filter(trivia));

        static IEnumerable<SyntaxTrivia> Filter(IEnumerable<SyntaxTrivia> input)
        {
            SyntaxTrivia? previous = null;
            foreach (var item in input)
            {
                if (item.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
                {
                    // Don't yield this, and don't yield previous either if it's whitespace
                    if (previous != null && !previous.Value.IsKind(SyntaxKind.WhitespaceTrivia))
                    {
                        yield return previous.Value;
                    }
                    previous = null;
                }
                else
                {
                    if (previous != null)
                    {
                        yield return previous.Value;
                    }
                    previous = item;
                }
            }

            if (previous != null)
            {
                yield return previous.Value;
            }
        }
    }
}
