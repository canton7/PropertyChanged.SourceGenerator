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

        public IEnumerable<TypeAnalysis> Analyse(HashSet<INamedTypeSymbol> typeSymbols)
        {
            var results = new Dictionary<INamedTypeSymbol, TypeAnalysis>(SymbolEqualityComparer.Default);

            foreach (var typeSymbol in typeSymbols)
            {
                Analyse(typeSymbol);
            }

            return results.Values;

            void Analyse(INamedTypeSymbol typeSymbol)
            {
                // If we've already analysed this one, return
                if (results.ContainsKey(typeSymbol))
                    return;

                // If we haven't analysed its base type yet, do that now. This will then happen recursively
                // Special System.Object, as we'll hit it a lot
                if (typeSymbol.BaseType != null
                    && typeSymbol.BaseType.SpecialType != SpecialType.System_Object
                    && !results.ContainsKey(typeSymbol.BaseType))
                {
                    Analyse(typeSymbol.BaseType);
                }

                // If we're not actually supposed to analyse this type, bail. We have to do this after the base
                // type analysis check above, as we can have TypeWeAnalyse depends on TypeWeDontAnalyse depends
                // on TypeWeAnalyse.
                if (!typeSymbols.Contains(typeSymbol))
                    return;

                // Right, we know we've analysed all of the base types by now. Fetch them
                var baseTypes = new List<TypeAnalysis>();
                for (var t = typeSymbol.BaseType; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
                {
                    if (results.TryGetValue(t, out var baseTypeAnalysis))
                    {
                        baseTypes.Add(baseTypeAnalysis);
                    }
                }

                // We're set! Analyse it
                var result = this.Analyse(typeSymbol, baseTypes);
                if (result != null)
                {
                    results.Add(typeSymbol, result);
                }
            }
        }

        private TypeAnalysis? Analyse(INamedTypeSymbol typeSymbol, List<TypeAnalysis> baseTypeAnalyses)
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

            // If we've got any base types, that will have the INPC interface and event on, for sure
            result.HasInpcInterface = baseTypeAnalyses.Count > 0
                || typeSymbol.AllInterfaces.Contains(this.inpcSymbol, SymbolEqualityComparer.Default);
            result.HasEvent = baseTypeAnalyses.Count > 0
                || typeSymbol.FindImplementationForInterfaceMember(this.propertyChangedSymbol) != null;
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
                switch (member)
                {
                    case IFieldSymbol field when this.GetNotifyAttribute(field) is { } attribute:
                        result.Members.Add(this.AnalyseField(field, attribute));
                        break;

                    case IPropertySymbol property when this.GetNotifyAttribute(property) is { } attribute:
                        result.Members.Add(this.AnalyseProperty(property, attribute));
                        break;
                }
            }

            // Now that we've got all members, we can do inter-member analysis

            // TODO: This could be smarter. We can ignore private members in base classes, for instance
            // We treat members we're generating on base types as already having been generated for the purposes of
            // these diagnostics
            var allDeclaredMemberNames = new HashSet<string>(TypeAndBaseTypes(typeSymbol)
                .SelectMany(x => x.MemberNames)
                .Concat(baseTypeAnalyses.SelectMany(x => x.Members.Select(y => y.Name))));
            for (int i = result.Members.Count - 1; i >= 0; i--)
            {
                var member = result.Members[i];
                if (allDeclaredMemberNames.Contains(member.Name))
                {
                    this.diagnostics.ReportMemberWithNameAlreadyExists(member.BackingMember, member.Name);
                    result.Members.RemoveAt(i);
                }
            }

            foreach (var collision in result.Members.GroupBy(x => x.Name).Where(x => x.Count() > 1))
            {
                var members = collision.ToList();
                for (int i = 0; i < members.Count; i++)
                {
                    var collidingMember = members[i == 0 ? 1 : 0];
                    this.diagnostics.ReportAnotherMemberHasSameGeneratedName(members[i].BackingMember, collidingMember.BackingMember, members[i].Name);
                    result.Members.Remove(members[i]);
                }
            }

            return result;
        }

        private MemberAnalysis AnalyseField(IFieldSymbol field, AttributeData notifyAttribute)
        {
            var result = this.AnalyseMember(field, field.Type, notifyAttribute);

            return result;
        }

        private MemberAnalysis AnalyseProperty(IPropertySymbol property, AttributeData notifyAttribute)
        {
            var result = this.AnalyseMember(property, property.Type, notifyAttribute);

            return result;
        }

        private MemberAnalysis AnalyseMember(
            ISymbol backingMember,
            ITypeSymbol type,
            AttributeData notifyAttribute)
        {
            string? explicitName = null;
            Accessibility getterAccessibility = Accessibility.Public;
            Accessibility setterAccessibility = Accessibility.Public;

            foreach (var arg in notifyAttribute.ConstructorArguments)
            {
                if (arg.Type?.SpecialType == SpecialType.System_String)
                {
                    explicitName = (string?)arg.Value;
                }
                else if (arg.Type?.Name == "Getter")
                {
                    getterAccessibility = (Accessibility)(int)arg.Value!;
                }
                else if (arg.Type?.Name == "Setter")
                {
                    setterAccessibility = (Accessibility)(int)arg.Value!;
                }
            }

            // We can't have a getter/setter being internal, and the setter/getter being protected
            if ((getterAccessibility == Accessibility.Internal && setterAccessibility == Accessibility.Protected) ||
                (getterAccessibility == Accessibility.Protected && setterAccessibility == Accessibility.Internal))
            {
                this.diagnostics.ReportIncomapatiblePropertyAccessibilities(type, notifyAttribute);
                getterAccessibility = Accessibility.ProtectedOrInternal;
                setterAccessibility = Accessibility.ProtectedOrInternal;
            }

            var result = new MemberAnalysis()
            {
                BackingMember = backingMember,
                Name = explicitName ?? this.TransformName(backingMember),
                Type = type,
                GetterAccessibility = getterAccessibility,
                SetterAccessibility = setterAccessibility,
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

        private string TransformName(ISymbol member)
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
    }
}
