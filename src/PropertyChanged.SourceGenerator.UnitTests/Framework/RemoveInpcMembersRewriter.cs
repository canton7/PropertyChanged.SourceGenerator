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
    private static readonly Configuration config = new();
    private readonly bool removeChanged;
    private readonly bool removeChanging;

    public static RemoveInpcMembersRewriter All { get; } = new(true, true);
    public static RemoveInpcMembersRewriter Changed { get; } = new(true, false);
    public static RemoveInpcMembersRewriter Changing { get; } = new(false, true);

    private RemoveInpcMembersRewriter(bool removeChanged, bool removeChanging)
    {
        this.removeChanged = removeChanged;
        this.removeChanging = removeChanging;
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (config.RaisePropertyChangedMethodNames.Contains(node.Identifier.ValueText))
        {
            return this.removeChanged ? null : node;
        }
        if (config.RaisePropertyChangingMethodNames.Contains(node.Identifier.ValueText))
        {
            return this.removeChanging ? null : node;
        }

        return base.VisitMethodDeclaration(node);
    }

    public override SyntaxNode? VisitEventFieldDeclaration(EventFieldDeclarationSyntax node) 
    {
        if (this.removeChanged && node.Declaration.Variables.Any(x => x.Identifier.ValueText == "PropertyChanged"))
        {
            return null;
        }
        if (this.removeChanging && node.Declaration.Variables.Any(x => x.Identifier.ValueText == "PropertyChanging"))
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
        if (this.removeChanged && node.Type is QualifiedNameSyntax changedSyntax && changedSyntax.ToString() == "global::System.ComponentModel.INotifyPropertyChanged")
        {
            return null;
        }
        if (this.removeChanging && node.Type is QualifiedNameSyntax changingSyntax && changingSyntax.ToString() == "global::System.ComponentModel.INotifyPropertyChanging")
        {
            return null;
        }

        return base.VisitSimpleBaseType(node);
    }
}
