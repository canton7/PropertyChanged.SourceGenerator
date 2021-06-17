using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PropertyChanged.SourceGenerator.Analysis
{
    public partial class Analyser
    {
        private readonly DiagnosticReporter diagnostics;
        private readonly Configuration config;
        private readonly Compilation compilation;
        private readonly INamedTypeSymbol? inpcSymbol;
        private readonly INamedTypeSymbol? propertyChangedEventHandlerSymbol;
        private readonly INamedTypeSymbol? propertyChangedEventArgsSymbol;
        private readonly INamedTypeSymbol notifyAttributeSymbol;
        private readonly INamedTypeSymbol alsoNotifyAttributeSymbol;
        private readonly INamedTypeSymbol dependsOnAttributeSymbol;
        private readonly INamedTypeSymbol isChangedAttributeSymbol;

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
                this.propertyChangedEventHandlerSymbol = compilation.GetTypeByMetadataName("System.ComponentModel.PropertyChangedEventHandler");
                this.propertyChangedEventArgsSymbol = compilation.GetTypeByMetadataName("System.ComponentModel.PropertyChangedEventArgs");
            }

            this.notifyAttributeSymbol = compilation.GetTypeByMetadataName("PropertyChanged.SourceGenerator.NotifyAttribute")
                ?? throw new InvalidOperationException("NotifyAttribute must have been added to assembly");
            this.alsoNotifyAttributeSymbol = compilation.GetTypeByMetadataName("PropertyChanged.SourceGenerator.AlsoNotifyAttribute")
                ?? throw new InvalidOperationException("AlsoNotifyAttribute must have been added to the assembly");
            this.dependsOnAttributeSymbol = compilation.GetTypeByMetadataName("PropertyChanged.SourceGenerator.DependsOnAttribute")
               ?? throw new InvalidOperationException("DependsOnAttribute must have been added to the assembly");
            this.isChangedAttributeSymbol = compilation.GetTypeByMetadataName("PropertyChanged.SourceGenerator.IsChangedAttribute")
               ?? throw new InvalidOperationException("IsChangedAttribute must have been added to the assembly");
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
                results.Add(typeSymbol, this.Analyse(typeSymbol, baseTypes));
            }
        }

        private TypeAnalysis Analyse(INamedTypeSymbol typeSymbol, List<TypeAnalysis> baseTypeAnalyses)
        {
            if (this.inpcSymbol == null)
                throw new InvalidOperationException();

            var result = new TypeAnalysis()
            {
                CanGenerate = true,
                TypeSymbol = typeSymbol,
            };

            if (!this.TryFindPropertyRaiseMethod(typeSymbol, result, baseTypeAnalyses))
            {
                result.CanGenerate = false;
            }

            // If we've got any base types we're generating partial types for , that will have the INPC interface
            // and event on, for sure
            result.HasInpcInterface = baseTypeAnalyses.Any(x => x.CanGenerate)
                || typeSymbol.AllInterfaces.Contains(this.inpcSymbol, SymbolEqualityComparer.Default);
            result.NullableContext = this.compilation.Options.NullableContextOptions;

            this.ResoveInheritedIsChanged(result, baseTypeAnalyses);

            foreach (var member in typeSymbol.GetMembers())
            {
                MemberAnalysis? memberAnalysis = null;
                switch (member)
                {
                    case IFieldSymbol field when this.GetNotifyAttribute(field) is { } attribute:
                        memberAnalysis = this.AnalyseField(field, attribute);
                        break;

                    case IPropertySymbol property when this.GetNotifyAttribute(property) is { } attribute:
                        memberAnalysis = this.AnalyseProperty(property, attribute);
                        break;

                    case var _ when member is IFieldSymbol or IPropertySymbol:
                        this.EnsureNoUnexpectedAttributes(member);
                        break;
                }

                if (memberAnalysis != null)
                {
                    result.Members.Add(memberAnalysis);
                }

                this.ResolveIsChangedMember(result, member, memberAnalysis);
            }

            // Now that we've got all members, we can do inter-member analysis

            this.ReportPropertyNameCollisions(result, baseTypeAnalyses);
            this.ResolveAlsoNotify(result, baseTypeAnalyses);
            this.ResolveDependsOn(result);

            bool isPartial = typeSymbol.DeclaringSyntaxReferences
                .Select(x => x.GetSyntax())
                .OfType<ClassDeclarationSyntax>()
                .Any(x => x.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
            if (!isPartial)
            {
                result.CanGenerate = false;
                if (result.Members.Count > 0)
                {
                    this.diagnostics.ReportTypeIsNotPartial(typeSymbol);
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

            string name = explicitName ?? this.TransformName(backingMember);
            var result = new MemberAnalysis()
            {
                BackingMember = backingMember,
                Name = name,
                Type = type,
                GetterAccessibility = getterAccessibility,
                SetterAccessibility = setterAccessibility,
                OnPropertyNameChanged = this.FindOnPropertyNameChangedMethod(backingMember.ContainingType, name, type, backingMember.ContainingType),
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

        private void ReportPropertyNameCollisions(TypeAnalysis typeAnalysis, List<TypeAnalysis> baseTypeAnalyses)
        {
            // TODO: This could be smarter. We can ignore private members in base classes, for instance
            // We treat members we're generating on base types as already having been generated for the purposes of
            // these diagnostics
            var allDeclaredMemberNames = new HashSet<string>(TypeAndBaseTypes(typeAnalysis.TypeSymbol)
                .SelectMany(x => x.MemberNames)
                .Concat(baseTypeAnalyses.SelectMany(x => x.Members.Select(y => y.Name))));
            for (int i = typeAnalysis.Members.Count - 1; i >= 0; i--)
            {
                var member = typeAnalysis.Members[i];
                if (allDeclaredMemberNames.Contains(member.Name))
                {
                    this.diagnostics.ReportMemberWithNameAlreadyExists(member.BackingMember, member.Name);
                    typeAnalysis.Members.RemoveAt(i);
                }
            }

            foreach (var collision in typeAnalysis.Members.GroupBy(x => x.Name).Where(x => x.Count() > 1))
            {
                var members = collision.ToList();
                for (int i = 0; i < members.Count; i++)
                {
                    var collidingMember = members[i == 0 ? 1 : 0];
                    this.diagnostics.ReportAnotherMemberHasSameGeneratedName(members[i].BackingMember, collidingMember.BackingMember, members[i].Name);
                    typeAnalysis.Members.Remove(members[i]);
                }
            }
        }

        private static IEnumerable<string?> ExtractAttributeStringParams(AttributeData attribute)
        {
            IEnumerable<string?> values;

            if (attribute.ConstructorArguments.Length == 1 &&
                attribute.ConstructorArguments[0].Kind == TypedConstantKind.Array &&
                !attribute.ConstructorArguments[0].Values.IsDefault)
            {
                values = attribute.ConstructorArguments[0].Values
                    .Where(x => x.Kind == TypedConstantKind.Primitive && x.Value is null or string)
                    .Select(x => x.Value)
                    .Cast<string?>();
            }
            else
            {
                values = attribute.ConstructorArguments
                    .Where(x => x.Kind == TypedConstantKind.Primitive && x.Value is null or string)
                    .Select(x => x.Value)
                    .Cast<string?>();
            }

            return values;
        }


        private void EnsureNoUnexpectedAttributes(ISymbol member)
        {
            foreach (var attribute in member.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, this.alsoNotifyAttributeSymbol))
                {
                    this.diagnostics.ReportAlsoNotifyAttributeNotValidOnMember(attribute, member);
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> TypeAndBaseTypes(INamedTypeSymbol type)
        {
            // Stop at 'object': no point in analysing that
            for (var t = type; t!.SpecialType != SpecialType.System_Object; t = t.BaseType)
            {
                yield return t;
            }
        }

        private static ITypeSymbol? GetMemberType(ISymbol member)
        {
            return member switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                _ => null,
            };
        }

        private AttributeData? GetNotifyAttribute(ISymbol member)
        {
            return member.GetAttributes().SingleOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, this.notifyAttributeSymbol));
        }
    }
}
