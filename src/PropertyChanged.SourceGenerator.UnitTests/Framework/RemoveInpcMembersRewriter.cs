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

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node) =>
        this.RewriteTypeDeclaration((TypeDeclarationSyntax)base.VisitClassDeclaration(node)!);

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node) =>
        this.RewriteTypeDeclaration((TypeDeclarationSyntax)base.VisitStructDeclaration(node)!);

    public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node) =>
        this.RewriteTypeDeclaration((TypeDeclarationSyntax)base.VisitRecordDeclaration(node)!);

    private TypeDeclarationSyntax RewriteTypeDeclaration(TypeDeclarationSyntax node)
    {
        if (node.BaseList?.Types.Count == 0)
        {
            // The newline is normally attached to the final item in the BaseList
            node = node.ReplaceNode(node.BaseList, node.BaseList.WithTrailingTrivia(SyntaxFactory.Whitespace("\r\n")));
            node = node.RemoveNode(node.BaseList!, SyntaxRemoveOptions.KeepTrailingTrivia)!;
            // Remove any trailing space from the class name
            node = node.WithIdentifier(node.Identifier.WithTrailingTrivia(default(SyntaxTriviaList)));
            if (node.TypeParameterList != null)
            {
                node = node.WithTypeParameterList(node.TypeParameterList.WithTrailingTrivia(default(SyntaxTriviaList)));
            }
        }

        return node;
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
