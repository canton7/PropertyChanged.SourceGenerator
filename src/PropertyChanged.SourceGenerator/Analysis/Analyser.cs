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
        private readonly Compilation compilation;
        private readonly INamedTypeSymbol? inpcSymbol;
        private readonly IEventSymbol? propertyChangedSymbol;
        private readonly INamedTypeSymbol notifyAttributeSymbol;

        public Analyser(DiagnosticReporter diagnostics, Configuration config, Compilation compilation)
        {
            this.diagnostics = diagnostics;
            this.config = config;
            this.compilation = compilation;

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

        public TypeAnalysis? Analyse(INamedTypeSymbol typeSymbol)
        {
            if (this.inpcSymbol == null || this.propertyChangedSymbol == null)
                throw new InvalidOperationException();

            bool isPartial = typeSymbol.DeclaringSyntaxReferences
                .Select(x => x.GetSyntax())
                .OfType<ClassDeclarationSyntax>()
                .Any(x => x.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
            if (!isPartial)
            {
                this.diagnostics.ReportTypeIsNotPartial(typeSymbol);
                return null;
            }

            var result = new TypeAnalysis()
            {
                TypeSymbol = typeSymbol
            };

            result.HasInpcInterface = typeSymbol.AllInterfaces.Contains(this.inpcSymbol, SymbolEqualityComparer.Default);
            result.HasEvent = typeSymbol.FindImplementationForInterfaceMember(this.propertyChangedSymbol) != null;
            result.HasOnPropertyChangedMethod = TypeAndBaseTypes(typeSymbol)
                .SelectMany(x => x.GetMembers(this.config.OnPropertyChangedMethodName))
                .OfType<IMethodSymbol>()
                .Any(x => x.Parameters.Length == 1 &&
                    x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                    x.TypeParameters.Length == 0 &&
                    (x.DeclaredAccessibility != Accessibility.Private || SymbolEqualityComparer.Default.Equals(x.ContainingType, typeSymbol)));
            result.NullableContext = this.compilation.Options.NullableContextOptions;

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
            var result = this.AnalyseMember(typeSymbol, field, field.Type, notifyAttribute);

            return result;
        }

        private MemberAnalysis AnalyseProperty(INamedTypeSymbol typeSymbol, IPropertySymbol property, AttributeData notifyAttribute)
        {
            var result = this.AnalyseMember(typeSymbol, property, property.Type, notifyAttribute);

            return result;
        }

        private MemberAnalysis AnalyseMember(
            INamedTypeSymbol typeSymbol,
            ISymbol backingMember,
            ITypeSymbol type,
            AttributeData notifyAttribute)
        {
            var result = new MemberAnalysis()
            {
                BackingMember = backingMember,
                Name = this.TransformName(typeSymbol, backingMember),
                Type = type,
            };

            if (type.IsReferenceType)
            {
                if (this.compilation.Options.NullableContextOptions.HasFlag(NullableContextOptions.Annotations) && type.NullableAnnotation == NullableAnnotation.None)
                {
                    result.NullableContextOverride = NullableContextOptions.Disable;
                }
                else if (this.compilation.Options.NullableContextOptions == NullableContextOptions.Disable && type.NullableAnnotation != NullableAnnotation.None)
                {
                    result.NullableContextOverride = NullableContextOptions.Annotations;
                }
            }

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
