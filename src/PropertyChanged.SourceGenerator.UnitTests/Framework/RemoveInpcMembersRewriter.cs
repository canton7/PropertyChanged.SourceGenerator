using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PropertyChanged.SourceGenerator.UnitTests.Framework;

public class RemoveInpcMembersRewriter : CSharpSyntaxRewriter
{
    public static RemoveInpcMembersRewriter Instance { get; } = new();

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Identifier.ValueText == "OnPropertyChanged")
        {
            return null;
        }

        return base.VisitMethodDeclaration(node);
    }

    public override SyntaxNode? VisitEventFieldDeclaration(EventFieldDeclarationSyntax node) 
    {
        if (node.Declaration.Variables.Any(x => x.Identifier.ValueText == "PropertyChanged"))
        {
            return null;
        }

        return base.VisitEventFieldDeclaration(node);
    }

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var rewritten = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;

        if (rewritten.BaseList?.Types.Count == 0)
        {
            // The newline is normally attached to the final item in the BaseList
            rewritten = rewritten.ReplaceNode(rewritten.BaseList, rewritten.BaseList.WithTrailingTrivia(SyntaxFactory.Whitespace("\r\n")));
            rewritten = rewritten.RemoveNode(rewritten.BaseList!, SyntaxRemoveOptions.KeepTrailingTrivia)!;
            // Remove any trailing space from the class name
            rewritten = rewritten.WithIdentifier(rewritten.Identifier.WithTrailingTrivia(default(SyntaxTriviaList)));
            if (rewritten.TypeParameterList != null)
            {
                rewritten = rewritten.WithTypeParameterList(rewritten.TypeParameterList.WithTrailingTrivia(default(SyntaxTriviaList)));
            }
        }

        return rewritten;
    }

    public override SyntaxNode? VisitSimpleBaseType(SimpleBaseTypeSyntax node)
    {
        if (node.Type is QualifiedNameSyntax syntax && syntax.ToString() == "global::System.ComponentModel.INotifyPropertyChanged")
        {
            return null;
        }

        return base.VisitSimpleBaseType(node);
    }
}
