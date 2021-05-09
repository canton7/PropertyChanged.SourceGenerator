using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PropertyChanged.SourceGenerator.UnitTests.Framework
{
    public class RemoveInpcMembersRewriter : CSharpSyntaxRewriter
    {
        public static RemoveInpcMembersRewriter Instance { get; } = new();

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node.Identifier.ValueText == "RaisePropertyChanged")
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
    }
}
