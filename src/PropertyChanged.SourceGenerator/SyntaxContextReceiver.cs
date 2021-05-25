using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PropertyChanged.SourceGenerator
{
    public class SyntaxContextReceiver : ISyntaxContextReceiver
    {
        public HashSet<INamedTypeSymbol> Types { get; } = new(SymbolEqualityComparer.Default);

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            switch (context.Node)
            {
                case FieldDeclarationSyntax fieldDeclaration:
                    foreach (var variable in fieldDeclaration.Declaration.Variables)
                    {
                        Process(variable);
                    }
                    break;
                case PropertyDeclarationSyntax propertyDeclaration:
                    Process(propertyDeclaration);
                    break;
            }

            void Process(SyntaxNode node)
            {
                if (context.SemanticModel.GetDeclaredSymbol(node) is { } symbol &&
                        symbol.GetAttributes().Any(x => 
                            x.AttributeClass?.ContainingNamespace.ToDisplayString() == "PropertyChanged.SourceGenerator"))
                {
                    this.Types.Add(symbol.ContainingType);
                }
            }
        }
    }
}
