using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PropertyChanged.SourceGenerator.Analysis
{
    public class Analyser
    {
        private readonly DiagnosticReporter diagnostics;
        private readonly Configuration config;
        private readonly INamedTypeSymbol? inpcSymbol;
        private readonly IEventSymbol? propertyChangedSymbol;
        private readonly INamedTypeSymbol notifyAttributeSymbol;

        public Analyser(DiagnosticReporter diagnostics, Configuration config, Compilation compilation)
        {
            this.diagnostics = diagnostics;
            this.config = config;

            this.inpcSymbol = compilation.GetTypeByMetadataName("System.ComponentModel.INotifyPropertyChanged");
            if (this.inpcSymbol == null)
            {
                this.diagnostics.ReportCouldNotFindInpc();
            }
            else
            {
                this.propertyChangedSymbol = this.inpcSymbol.GetMembers().OfType<IEventSymbol>().Single(x => x.Name == "PropertyChanged");
            }

            this.notifyAttributeSymbol = compilation.GetTypeByMetadataName("PropertyChanged.SourceGenerator.NotifyAttribute")
                ?? throw new InvalidOperationException("NotifyAttribute must have been added to assembly");
        }

        public TypeAnalysis Analyse(INamedTypeSymbol typeSymbol)
        {
            // Should have been checked by caller
            if (this.inpcSymbol == null || this.propertyChangedSymbol == null)
                throw new InvalidOperationException();

            var result = new TypeAnalysis()
            {
                TypeSymbol = typeSymbol
            };

            result.IsPartial = typeSymbol.DeclaringSyntaxReferences
                .Select(x => x.GetSyntax())
                .OfType<ClassDeclarationSyntax>()
                .Any(x => x.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
            result.HasInpcInterface = typeSymbol.AllInterfaces.Contains(this.inpcSymbol, SymbolEqualityComparer.Default);
            result.HasEvent = typeSymbol.FindImplementationForInterfaceMember(this.propertyChangedSymbol) != null;
            result.HasOnPropertyChangedMethod = TypeAndBaseTypes(typeSymbol)
                .SelectMany(x => x.GetMembers(this.config.OnPropertyChangedMethodName))
                .OfType<IMethodSymbol>()
                .Any(x => x.Parameters.Length == 1 &&
                    x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                    x.TypeParameters.Length == 0 &&
                    (x.DeclaredAccessibility != Accessibility.Private || SymbolEqualityComparer.Default.Equals(x.ContainingType, typeSymbol)));

            foreach (var member in typeSymbol.GetMembers())
            {
                try
                {
                    switch (member)
                    {
                        case IFieldSymbol field when this.GetNotifyAttribute(field) is { } attribute:
                            result.Members.Add(this.AnalyseField(typeSymbol, field, attribute));
                            break;

                        case IPropertySymbol property when this.GetNotifyAttribute(property) is { } attribute:
                            result.Members.Add(this.AnalyseProperty(typeSymbol, property, attribute));
                            break;
                    }
                }
                catch (MemberAnalysisFailedException) { }
            }

            return result;
        }

        private MemberAnalysis AnalyseField(INamedTypeSymbol typeSymbol, IFieldSymbol field, AttributeData notifyAttribute)
        {
            var result = this.AnalyseMember(typeSymbol, field, notifyAttribute);
            result.Type = field.Type;

            return result;
        }

        private MemberAnalysis AnalyseProperty(INamedTypeSymbol typeSymbol, IPropertySymbol property, AttributeData notifyAttribute)
        {
            var result = this.AnalyseMember(typeSymbol, property, notifyAttribute);
            result.Type = property.Type;

            return result;
        }

        private MemberAnalysis AnalyseMember(INamedTypeSymbol typeSymbol, ISymbol symbol, AttributeData notifyAttribute)
        {
            var result = new MemberAnalysis
            {
                BackingMember = symbol,
                Name = this.TransformName(typeSymbol, symbol)
            };

            return result;
        }

        private string TransformName(INamedTypeSymbol typeSymbol, ISymbol member)
        {
            string name = member.Name;
            foreach (string removePrefix in this.config.RemovePrefixes)
            {
                if (name.StartsWith(removePrefix))
                {
                    name = name.Substring(removePrefix.Length);
                }
            }
            foreach (string removeSuffix in this.config.RemoveSuffixes)
            {
                if (name.EndsWith(removeSuffix))
                {
                    name = name.Substring(0, name.Length - removeSuffix.Length);
                }
            }
            if (this.config.AddPrefix != null)
            {
                name = this.config.AddPrefix + name;
            }
            if (this.config.AddSuffix != null)
            {
                name += this.config.AddSuffix;
            }
            switch (this.config.FirstLetterCapitalisation)
            {
                case Capitalisation.None:
                    break;
                case Capitalisation.Uppercase:
                    name = char.ToUpper(name[0]) + name.Substring(1);
                    break;
                case Capitalisation.Lowercase:
                    name = char.ToLower(name[0]) + name.Substring(1);
                    break;
            }

            if (typeSymbol.MemberNames.Contains(name))
            {
                this.diagnostics.ReportMemberRenameResultedInConflict(member, name);
                throw new MemberAnalysisFailedException();
            }

            return name;
        }

        private static IEnumerable<INamedTypeSymbol> TypeAndBaseTypes(INamedTypeSymbol type)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                yield return t;
            }
        }

        private AttributeData? GetNotifyAttribute(ISymbol member)
        {
            return member.GetAttributes().SingleOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, this.notifyAttributeSymbol));
        }

        private class MemberAnalysisFailedException : Exception { }
    }
}
