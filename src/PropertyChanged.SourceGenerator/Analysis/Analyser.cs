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
        private readonly INamedTypeSymbol? propertyChangedEventHandlerSymbol;
        private readonly INamedTypeSymbol? propertyChangedEventArgsSymbol;
        private readonly INamedTypeSymbol notifyAttributeSymbol;
        private readonly INamedTypeSymbol alsoNotifyAttributeSymbol;
        private readonly INamedTypeSymbol dependsOnAttributeSymbol;

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
            if (this.inpcSymbol == null)
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

            if (!this.TryFindPropertyRaiseMethod(typeSymbol, result, baseTypeAnalyses))
            {
                return null;
            }

            // If we've got any base types, that will have the INPC interface and event on, for sure
            result.HasInpcInterface = baseTypeAnalyses.Count > 0
                || typeSymbol.AllInterfaces.Contains(this.inpcSymbol, SymbolEqualityComparer.Default);
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

                    case var _ when member is IFieldSymbol or IPropertySymbol:
                        this.EnsureNoUnexpectedAttributes(member);
                        break;
                }
            }

            // Now that we've got all members, we can do inter-member analysis

            this.ReportPropertyNameCollisions(result, baseTypeAnalyses);
            this.ResolveAlsoNotify(result, baseTypeAnalyses);
            this.ResolveDependsOn(result);

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

        private bool TryFindPropertyRaiseMethod(
            INamedTypeSymbol typeSymbol,
            TypeAnalysis typeAnalysis,
            IReadOnlyList<TypeAnalysis> baseTypeAnalyses)
        {
            // Try and find out how we raise the PropertyChanged event
            // 1. If noone's defined the PropertyChanged event yet, we'll define it ourselves
            // 2. Otherwise, try and find a method to raise the event:
            //   a. If PropertyChanged is in a base class, we'll need to abort if we can't find one
            //   b. If PropertyChanged is in our class, we'll just define one and call it

            // They might have defined the event but not the interface, so we'll just look for the event by its
            // signature
            var eventSymbol = TypeAndBaseTypes(typeSymbol)
                .SelectMany(x => x.GetMembers("PropertyChanged"))
                .OfType<IEventSymbol>()
                .FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.Type, this.propertyChangedEventHandlerSymbol) &&
                    !x.IsStatic);

            // If there's no event, the base type in our hierarchy is defining it
            typeAnalysis.RequiresEvent = eventSymbol == null && baseTypeAnalyses.Count == 0;

            // Try and find a method with a name we recognise and a signature we know how to call
            // We prioritise the method name over things like the signature or where in the type hierarchy
            // it is. One we've found any method with a name we're looking for, stop: it's most likely they've 
            // just messed up the signature
            RaisePropertyChangedMethodSignature? signature = null;
            string? methodName = null;
            foreach (string name in this.config.RaisePropertyChangedMethodNames)
            {
                var methods = TypeAndBaseTypes(typeSymbol)
                    .SelectMany(x => x.GetMembers(name))
                    .OfType<IMethodSymbol>()
                    .Where(x => !x.IsOverride && !x.IsStatic)
                    .ToList();
                if (methods.Count > 0)
                {
                    signature = FindCallableOverload(methods);
                    if (signature != null)
                    {
                        methodName = name;
                    }
                    else
                    {
                        this.diagnostics.ReportCouldNotFindCallableRaisePropertyChangedOverload(typeSymbol, name);
                        return false;
                    }
                    break;
                }
            }

            if (signature != null)
            {
                // We found a method which we know how to call
                typeAnalysis.RequiresRaisePropertyChangedMethod = false;
                typeAnalysis.RaisePropertyChangedMethodName = methodName!;
                typeAnalysis.RaisePropertyChangedMethodSignature = signature.Value;
            }
            else
            {
                // The base type in our hierarchy is defining its own
                // Make sure that that type can actually access the event, if it's pre-existing
                if (eventSymbol != null && baseTypeAnalyses.Count == 0 && 
                    !SymbolEqualityComparer.Default.Equals(eventSymbol.ContainingType, typeSymbol))
                {
                    this.diagnostics.ReportCouldNotFindRaisePropertyChangedMethod(typeSymbol);
                    return false;
                }
                
                typeAnalysis.RequiresRaisePropertyChangedMethod = baseTypeAnalyses.Count == 0;
                typeAnalysis.RaisePropertyChangedMethodName = this.config.RaisePropertyChangedMethodNames[0];
                typeAnalysis.RaisePropertyChangedMethodSignature = RaisePropertyChangedMethodSignature.Default;
            }

            return true;

            RaisePropertyChangedMethodSignature? FindCallableOverload(List<IMethodSymbol> methods)
            {
                // We care about the order in which we choose an overload, which unfortunately means we're quadratic
                if (methods.Any(x => IsAccessibleNormalInstanceMethod(x) &&
                    x.Parameters.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, this.propertyChangedEventArgsSymbol) &&
                    IsNormalParameter(x.Parameters[0])))
                {
                    return new RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType.PropertyChangedEventArgs, hasOldAndNew: false);
                }

                if (methods.Any(x => IsAccessibleNormalInstanceMethod(x) &&
                    x.Parameters.Length == 1 &&
                    x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                    IsNormalParameter(x.Parameters[0])))
                {
                    return new RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType.String, hasOldAndNew: false);
                }

                if (methods.Any(x => IsAccessibleNormalInstanceMethod(x) &&
                    x.Parameters.Length == 3 &&
                    SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, this.propertyChangedEventArgsSymbol) &&
                    IsNormalParameter(x.Parameters[0]) &&
                    x.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
                    IsNormalParameter(x.Parameters[1]) &&
                    x.Parameters[2].Type.SpecialType == SpecialType.System_Object &&
                    IsNormalParameter(x.Parameters[2])))
                {
                    return new RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType.PropertyChangedEventArgs, hasOldAndNew: true);
                }
                if (methods.Any(x => IsAccessibleNormalInstanceMethod(x) &&
                    x.Parameters.Length == 3 &&
                    x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                    IsNormalParameter(x.Parameters[0]) &&
                    x.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
                    IsNormalParameter(x.Parameters[1]) &&
                    x.Parameters[2].Type.SpecialType == SpecialType.System_Object &&
                    IsNormalParameter(x.Parameters[2])))
                {
                    return new RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType.String, hasOldAndNew: true);
                }

                return null;
            }

            bool IsAccessibleNormalInstanceMethod(IMethodSymbol method) =>
                !method.IsGenericMethod &&
                method.ReturnsVoid &&
                this.compilation.IsSymbolAccessibleWithin(method, typeSymbol);

            bool IsNormalParameter(IParameterSymbol parameter) =>
                parameter.RefKind == RefKind.None;
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

        private void ResolveAlsoNotify(TypeAnalysis typeAnalysis, List<TypeAnalysis> baseTypeAnalyses)
        {
            // We've already warned if there are AlsoNotify attributes on members that we haven't analysed
            foreach (var member in typeAnalysis.Members)
            {
                var alsoNotifyAttributes = member.BackingMember.GetAttributes().Where(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, this.alsoNotifyAttributeSymbol));
                foreach (var attribute in alsoNotifyAttributes)
                {
                    var alsoNotifyValues = ExtractAttributeStringParams(attribute);

                    // We only allow them to use the property name. If we didn't, consider:
                    // 1. Derived class has the same property name as base class (shadowed)
                    // 2. Derived class has the same field name as a base class (generating different properties)
                    // 3. Derived class has a field with the same name as a base class' property
                    // 4. Derived class has a property with the same name as a base class' field

                    foreach (string? alsoNotify in alsoNotifyValues)
                    {
                        // Remember that we're probably, but not necessarily, notifying a property which we're also
                        // generating.
                        // Allow null and emptystring as special cases
                        if (alsoNotify == member.Name)
                        {
                            this.diagnostics.ReportAlsoNotifyForSelf(alsoNotify, attribute, member.BackingMember);
                        }
                        else
                        {
                            ITypeSymbol? foundCallableType = null;
                            if (!string.IsNullOrEmpty(alsoNotify))
                            {
                                var foundType = baseTypeAnalyses.Prepend(typeAnalysis)
                                    .SelectMany(x => x.Members)
                                    .FirstOrDefault(x => x.Name == alsoNotify)?
                                    .Type;
                                foundType ??= TypeAndBaseTypes(typeAnalysis.TypeSymbol)
                                    .SelectMany(x => x.GetMembers(alsoNotify!))
                                    .OfType<IPropertySymbol>()
                                    .FirstOrDefault(x => this.compilation.IsSymbolAccessibleWithin(x, typeAnalysis.TypeSymbol))?
                                    .Type;
                                foundCallableType = foundType;
                                // Also let them use "Item[]", if an indexer exists
                                if (foundType == null && alsoNotify!.EndsWith("[]"))
                                {
                                    string indexerName = alsoNotify.Substring(0, alsoNotify.Length - "[]".Length);
                                    foundType = TypeAndBaseTypes(typeAnalysis.TypeSymbol)
                                        .SelectMany(x => x.GetMembers("this[]"))
                                        .OfType<IPropertySymbol>()
                                        .FirstOrDefault(x => x.IsIndexer && x.MetadataName == indexerName)?
                                        .Type;
                                }
                                if (foundType == null)
                                {
                                    this.diagnostics.ReportAlsoNotifyPropertyDoesNotExist(alsoNotify!, attribute, member.BackingMember);
                                }
                            }
                            member.AddAlsoNotify(new AlsoNotifyMember(alsoNotify, foundCallableType));
                        }
                    }
                }
            }
        }

        private void ResolveDependsOn(TypeAnalysis typeAnalysis)
        {
            var lookups = new TypeAnalysisLookups(typeAnalysis);
            foreach (var member in typeAnalysis.TypeSymbol.GetMembers().Where(x => x is IFieldSymbol or IPropertySymbol))
            {
                var dependsOnAttributes = member.GetAttributes()
                    .Where(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, this.dependsOnAttributeSymbol))
                    .ToList();
                if (dependsOnAttributes.Count > 0)
                {
                    this.ResolveManualDependsOn(lookups, member, dependsOnAttributes);
                }
                else if (member is IPropertySymbol propertySymbol)
                {
                    this.ResolveAutoDependsOn(propertySymbol, lookups);
                }
            }
        }

        private void ResolveManualDependsOn(TypeAnalysisLookups lookups, ISymbol member, List<AttributeData> dependsOnAttributes)
        {
            foreach (var attribute in dependsOnAttributes)
            {
                var dependsOnValues = ExtractAttributeStringParams(attribute);
                foreach (string? dependsOn in dependsOnValues)
                {
                    // This must be the name of a property we're generating
                    if (dependsOn != null && lookups.TryGet(dependsOn, out var dependsOnMember))
                    {
                        // If we're generating this property, make sure we use the generated name...
                        if (lookups.TryGet(member, out var memberAnalysis))
                        {
                            dependsOnMember.AddAlsoNotify(new AlsoNotifyMember(memberAnalysis.Name, memberAnalysis.Type));
                        }
                        else if (member is IPropertySymbol property)
                        {
                            dependsOnMember.AddAlsoNotify(new AlsoNotifyMember(property.Name, property.Type));
                        }
                        else
                        {
                            // If we're not generating anything from this, and it's not a property, raise
                            this.diagnostics.RaiseDependsOnAppliedToFieldWithoutNotify(attribute, member);
                        }
                    }
                    else
                    {
                        this.diagnostics.RaiseDependsOnPropertyDoesNotExist(dependsOn, attribute, member);
                    }
                }
            }
        }

        private void ResolveAutoDependsOn(IPropertySymbol member, TypeAnalysisLookups lookups)
        {
            if (member.GetMethod?.Locations.FirstOrDefault() is { } location &&
                location.SourceTree?.GetRoot()?.FindNode(location.SourceSpan) is { } getterNode)
            {
                // Annoyingly, we're looking for references to properties which don't actually exist yet
                // We'll therefore have to do this entirely by name. We're looking for IdentifierNames which we
                // recognise.
                // We're looking for accesses which aren't part of a MemberAccessExpression ('Bar'), or are and the
                // left side is 'this' ('this.Bar').
                // Once we've got this, discount:
                // - pre/post increment/decrement, as this isn't a get
                // - Assignment, as this isn't a get
                foreach (var node in getterNode.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                {
                    if (!lookups.TryGet(node.Identifier.ValueText, out var memberAnalysis))
                        continue;

                    var parent = node.Parent;
                    // If this is the RHS of a member access (this.Bar or foo.Bar)
                    if (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node)
                    {
                        if (!memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression) ||
                            !memberAccess.Expression.IsKind(SyntaxKind.ThisExpression))
                            continue;
                        parent = memberAccess.Parent;
                    }

                    switch (parent?.Kind())
                    {
                        case SyntaxKind.PreIncrementExpression:
                        case SyntaxKind.PostIncrementExpression:
                        case SyntaxKind.PreDecrementExpression:
                        case SyntaxKind.PostDecrementExpression:
                            continue;
                    }

                    if (parent is AssignmentExpressionSyntax)
                        continue;

                    // It's probably a property access
                    memberAnalysis.AddAlsoNotify(new AlsoNotifyMember(member.Name, member.Type));
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
