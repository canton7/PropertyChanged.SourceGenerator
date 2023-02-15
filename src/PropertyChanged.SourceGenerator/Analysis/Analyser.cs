using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static PropertyChanged.SourceGenerator.Analysis.Utils;

namespace PropertyChanged.SourceGenerator.Analysis;

public partial class Analyser
{
    private readonly DiagnosticReporter diagnostics;
    private readonly Compilation compilation;
    private readonly NullableContextOptions nullableContextOptions;
    private readonly ConfigurationParser configurationParser;

    private readonly PropertyChangedInterfaceAnalyser? propertyChangedInterfaceAnalyser;
    private readonly PropertyChangingInterfaceAnalyser? propertyChangingInterfaceAnalyser;

    private INamedTypeSymbol? notifyAttributeSymbolCache;
    private INamedTypeSymbol notifyAttributeSymbol =>
        this.notifyAttributeSymbolCache ??= this.compilation.GetTypeByMetadataName("PropertyChanged.SourceGenerator.NotifyAttribute")
            ?? throw new InvalidOperationException("NotifyAttribute must have been added to assembly");

    private INamedTypeSymbol? alsoNotifyAttributeSymbolCache;
    private INamedTypeSymbol alsoNotifyAttributeSymbol => this.alsoNotifyAttributeSymbolCache ??= 
        this.compilation.GetTypeByMetadataName("PropertyChanged.SourceGenerator.AlsoNotifyAttribute")
            ?? throw new InvalidOperationException("AlsoNotifyAttribute must have been added to the assembly");

    private INamedTypeSymbol? dependsOnAttributeSymbolCache;
    private INamedTypeSymbol dependsOnAttributeSymbol => this.dependsOnAttributeSymbolCache ??=
        this.compilation.GetTypeByMetadataName("PropertyChanged.SourceGenerator.DependsOnAttribute")
           ?? throw new InvalidOperationException("DependsOnAttribute must have been added to the assembly");

    private INamedTypeSymbol? isChangedAttributeSymbolCache;
    private INamedTypeSymbol isChangedAttributeSymbol => this.isChangedAttributeSymbolCache ??= 
        this.compilation.GetTypeByMetadataName("PropertyChanged.SourceGenerator.IsChangedAttribute")
           ?? throw new InvalidOperationException("IsChangedAttribute must have been added to the assembly");

    private INamedTypeSymbol? propertyAttributeSymbolCache;
    private INamedTypeSymbol propertyAttributeSymbol => this.propertyAttributeSymbolCache ??=
        this.compilation.GetTypeByMetadataName("PropertyChanged.SourceGenerator.PropertyAttributeAttribute")
           ?? throw new InvalidOperationException("PropertyAttributeAttribute must have been added to the assembly");

    public Analyser(
        DiagnosticReporter diagnostics,
        Compilation compilation,
        NullableContextOptions nullableContextOptions,
        ConfigurationParser configurationParser)
    {
        this.diagnostics = diagnostics;
        this.compilation = compilation;
        this.nullableContextOptions = nullableContextOptions;
        this.configurationParser = configurationParser;

        var inpchangedSymbol = compilation.GetTypeByMetadataName("System.ComponentModel.INotifyPropertyChanged");
        if (inpchangedSymbol == null)
        {
            this.diagnostics.ReportCouldNotFindInpc();
        }
        else
        {
            // Fetching these symbols once for all types is probably cheaper than calculating the fully-qualified metadata name for each
            // symbol we want to test
            var propertyChangedEventArgsSymbol = compilation.GetTypeByMetadataName("System.ComponentModel.PropertyChangedEventArgs");

            var inpchangingSymbol = compilation.GetTypeByMetadataName("System.ComponentModel.INotifyPropertyChanging"); ;
            var propertyChangingEventArgsSymbol = compilation.GetTypeByMetadataName("System.ComponentModel.PropertyChangingEventArgs");
        
            this.propertyChangedInterfaceAnalyser = new(inpchangedSymbol, "System.ComponentModel.PropertyChangedEventHandler", propertyChangedEventArgsSymbol!, this.diagnostics, compilation);
            this.propertyChangingInterfaceAnalyser = new(inpchangingSymbol!, "System.ComponentModel.PropertyChangingEventHandler", propertyChangingEventArgsSymbol!, this.diagnostics, compilation);
        }
    }

    public IEnumerable<TypeAnalysis> Analyse(IEnumerable<INamedTypeSymbol> typeSymbols)
    {
        // This starts off with a null entry for each type symbol, indicating that we're supposed to analyse
        // this type but havent't yet done so. So we analyse types, we'll populate the values.
        // We expect duplicates in typeSymbols, so we can't use ToDictionary
        var results = new Dictionary<INamedTypeSymbol, TypeAnalysisBuilder?>(SymbolEqualityComparer.Default);
        foreach (var typeSymbol in typeSymbols)
        {
            results[typeSymbol.OriginalDefinition] = null;
        }

        foreach (var typeSymbol in results.Keys)
        {
            Analyse(typeSymbol);
        }

        return results.Values.Select(x => x!.Build());

        void Analyse(INamedTypeSymbol typeSymbol)
        {
            // Make sure it's not e.g. C<Foo>, rather C<T>
            Debug.Assert(SymbolEqualityComparer.Default.Equals(typeSymbol, typeSymbol.OriginalDefinition));

            // If we've already analysed this one, return
            if (results.TryGetValue(typeSymbol, out var analysis) && analysis != null)
                return;

            // If we haven't analysed its base type yet, do that now. This will then happen recursively
            // Special-case System.Object, as we'll hit it a lot
            // Use OriginalDefinition here, in case the child inerits from e.g. Base<Foo>: we want to analyse
            // Base<T>
            if (typeSymbol.BaseType != null
                && typeSymbol.BaseType.SpecialType != SpecialType.System_Object
                && results.TryGetValue(typeSymbol.BaseType.OriginalDefinition, out var existingAnalysis)
                && existingAnalysis == null)
            {
                Analyse(typeSymbol.BaseType.OriginalDefinition);
            }

            // If we're not actually supposed to analyse this type, bail. We have to do this after the base
            // type analysis check above, as we can have TypeWeAnalyse depends on TypeWeDontAnalyse depends
            // on TypeWeAnalyse.
            if (!results.ContainsKey(typeSymbol))
                return;

            // Right, we know we've analysed all of the base types by now. Fetch them
            var baseTypes = new List<TypeAnalysisBuilder>();
            for (var t = typeSymbol.BaseType?.OriginalDefinition; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType?.OriginalDefinition)
            {
                if (results.TryGetValue(t, out var baseTypeAnalysis))
                {
                    Debug.Assert(baseTypeAnalysis != null);
                    baseTypes.Add(baseTypeAnalysis!);
                }
            }

            // We're set! Analyse it
            results[typeSymbol] = this.Analyse(typeSymbol, baseTypes);
        }
    }

    private TypeAnalysisBuilder Analyse(INamedTypeSymbol typeSymbol, List<TypeAnalysisBuilder> baseTypeAnalyses)
    {
        var typeAnalysis = new TypeAnalysisBuilder()
        {
            CanGenerate = true,
            TypeSymbol = typeSymbol,
        };

        if (baseTypeAnalyses.FirstOrDefault()?.HadException == true)
        {
            // If we failed to analyse the base type because of an exception, we don't stand a chance. Bail now.
            this.diagnostics.ReportUnhandledExceptionOnParent(typeSymbol);
            typeAnalysis.HadException = true;
            typeAnalysis.CanGenerate = false;
        }
        else
        {
            try
            {
                this.AnalyseInner(typeAnalysis, baseTypeAnalyses);
            }
            catch (Exception e)
            {
                this.diagnostics.ReportUnhandledException(typeSymbol, e);
                typeAnalysis.HadException = true;
                typeAnalysis.CanGenerate = false;
            }
        }

        return typeAnalysis;
    }

    private void AnalyseInner(TypeAnalysisBuilder typeAnalysis, List<TypeAnalysisBuilder> baseTypeAnalyses)
    {
        if (this.propertyChangedInterfaceAnalyser == null)
            throw new InvalidOperationException();

        var config = this.configurationParser.Parse(typeAnalysis.TypeSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree, this.diagnostics);

        typeAnalysis.NullableContext = this.nullableContextOptions;

        typeAnalysis.INotifyPropertyChanged = this.propertyChangedInterfaceAnalyser.CreateInterfaceAnalysis(typeAnalysis.TypeSymbol, baseTypeAnalyses, config);
        typeAnalysis.INotifyPropertyChanging = this.propertyChangingInterfaceAnalyser!.CreateInterfaceAnalysis(typeAnalysis.TypeSymbol, baseTypeAnalyses, config);
        InterfaceAnalyser.PopulateRaiseMethodNameIfEmpty(typeAnalysis.INotifyPropertyChanged, typeAnalysis.INotifyPropertyChanging, config);
        this.ResoveInheritedIsChanged(typeAnalysis, baseTypeAnalyses);

        foreach (var member in typeAnalysis.TypeSymbol.GetMembers().Where(x => !x.IsImplicitlyDeclared))
        {
            MemberAnalysisBuilder? memberAnalysis = null;
            switch (member)
            {
                case IFieldSymbol field when this.GetNotifyAttribute(field) is { } attribute:
                    memberAnalysis = this.AnalyseField(field, attribute, config);
                    break;

                case IPropertySymbol property when this.GetNotifyAttribute(property) is { } attribute:
                    memberAnalysis = this.AnalyseProperty(property, attribute, config);
                    break;

                case var _ when member is IFieldSymbol or IPropertySymbol:
                    this.EnsureNoUnexpectedAttributes(member);
                    break;
            }

            if (memberAnalysis != null)
            {
                typeAnalysis.Members.Add(memberAnalysis);
            }

            this.ResolveIsChangedMember(typeAnalysis, member, memberAnalysis);
        }

        // Now that we've got all members, we can do inter-member analysis

        this.ReportPropertyNameCollisions(typeAnalysis, baseTypeAnalyses);
        this.ResolveAlsoNotify(typeAnalysis, baseTypeAnalyses);
        this.ResolveDependsOn(typeAnalysis, config);

        if (!IsPartial(typeAnalysis.TypeSymbol))
        {
            typeAnalysis.CanGenerate = false;
            if (typeAnalysis.Members.Count > 0)
            {
                this.diagnostics.ReportTypeIsNotPartial(typeAnalysis.TypeSymbol);
            }
        }

        for (var outerType = typeAnalysis.TypeSymbol.ContainingType; outerType != null; outerType = outerType.ContainingType)
        {
            if (!IsPartial(outerType))
            {
                typeAnalysis.CanGenerate = false;
                if (typeAnalysis.Members.Count > 0)
                {
                    this.diagnostics.ReportOuterTypeIsNotPartial(outerType, typeAnalysis.TypeSymbol);
                }
            }
        }

        bool IsPartial(INamedTypeSymbol type) =>
            type.DeclaringSyntaxReferences.Any(x =>
                x.GetSyntax() is TypeDeclarationSyntax syntax &&
                syntax.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    private MemberAnalysisBuilder? AnalyseField(IFieldSymbol field, AttributeData notifyAttribute, Configuration config)
    {
        if (field.IsReadOnly)
        {
            this.diagnostics.RaiseReadonlyBackingMember(field);
            return null;
        }
        var result = this.AnalyseMember(field, field.Type, notifyAttribute, config);
        return result;
    }

    private MemberAnalysisBuilder? AnalyseProperty(IPropertySymbol property, AttributeData notifyAttribute, Configuration config)
    {
        if (property.GetMethod == null || property.SetMethod == null)
        {
            this.diagnostics.RaiseBackingPropertyMustHaveGetterAndSetter(property);
            return null;
        }
        var result = this.AnalyseMember(property, property.Type, notifyAttribute, config);
        return result;
    }

    private MemberAnalysisBuilder? AnalyseMember(
        ISymbol backingMember,
        ITypeSymbol type,
        AttributeData notifyAttribute,
        Configuration config)
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

        bool isVirtual = false;
        foreach (var arg in notifyAttribute.NamedArguments)
        {
            if (arg.Key == "IsVirtual")
            {
                isVirtual = (bool)arg.Value.Value!;
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

        string name = explicitName ?? this.TransformName(backingMember, config);
        var result = new MemberAnalysisBuilder()
        {
            BackingMember = backingMember,
            Name = name,
            Type = type,
            IsVirtual = isVirtual,
            GetterAccessibility = getterAccessibility,
            SetterAccessibility = setterAccessibility,
            OnPropertyNameChanged = this.propertyChangedInterfaceAnalyser!.FindOnPropertyNameChangedMethod(backingMember.ContainingType, name, type, backingMember.ContainingType),
            OnPropertyNameChanging = this.propertyChangingInterfaceAnalyser!.FindOnPropertyNameChangedMethod(backingMember.ContainingType, name, type, backingMember.ContainingType),
            AttributesForGeneratedProperty = this.GetAttributesForGeneratedProperty(backingMember),
            DocComment = ParseDocComment(backingMember.GetDocumentationCommentXml()),
        };

        if (type.IsReferenceType)
        {
            if (this.nullableContextOptions.HasFlag(NullableContextOptions.Annotations) && type.NullableAnnotation == NullableAnnotation.None)
            {
                result.NullableContextOverride = NullableContextOptions.Disable;
            }
            else if (this.nullableContextOptions == NullableContextOptions.Disable && type.NullableAnnotation != NullableAnnotation.None)
            {
                result.NullableContextOverride = NullableContextOptions.Annotations;
            }
        }

        return result;
    }

    private string TransformName(ISymbol member, Configuration config)
    {
        string name = member.Name;
        foreach (string removePrefix in config.RemovePrefixes)
        {
            if (name.StartsWith(removePrefix))
            {
                name = name.Substring(removePrefix.Length);
            }
        }
        foreach (string removeSuffix in config.RemoveSuffixes)
        {
            if (name.EndsWith(removeSuffix))
            {
                name = name.Substring(0, name.Length - removeSuffix.Length);
            }
        }
        if (config.AddPrefix != null)
        {
            name = config.AddPrefix + name;
        }
        if (config.AddSuffix != null)
        {
            name += config.AddSuffix;
        }
        switch (config.FirstLetterCapitalisation)
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

    private void ReportPropertyNameCollisions(TypeAnalysisBuilder typeAnalysis, List<TypeAnalysisBuilder> baseTypeAnalyses)
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
            if (attribute.AttributeClass?.Name == "AlsoNotifyAttribute" && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, this.alsoNotifyAttributeSymbol))
            {
                this.diagnostics.ReportAlsoNotifyAttributeNotValidOnMember(attribute, member);
            }
            else if (attribute.AttributeClass?.Name == "PropertyAttributeAttribute" && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, this.propertyAttributeSymbol))
            {
                this.diagnostics.ReportAlsoNotifyAttributeNotValidOnMember(attribute, member);
            }
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

    private List<string>? GetAttributesForGeneratedProperty(ISymbol member)
    {
        List<string>? result = null;
        var attributes = member.GetAttributes().Where(x => x.AttributeClass?.Name == "PropertyAttributeAttribute" && SymbolEqualityComparer.Default.Equals(x.AttributeClass, this.propertyAttributeSymbol));
        foreach (var attribute in attributes)
        {
            if (attribute.ConstructorArguments.ElementAtOrDefault(0).Value is string str)
            {
                // If they haven't put [ and ] around it, be nice
                if (!str.StartsWith("[") && !str.EndsWith("]"))
                {
                    str = "[" + str + "]";
                }
                result ??= new();
                result.Add(str);
            }
        }
        return result;
    }

    private static string[]? ParseDocComment(string? xml)
    {
        string? comment = ParseDocCommentXml(xml);
        if (comment == null)
        {
            return null;
        }

        string[] lines = comment.Split('\n');
        if (lines.Length == 0)
        {
            return null;
        }

        string leadingWhitespace = "";
        for (int i = 0; i < lines[0].Length; i++)
        {
            if (lines[0][i] != ' ')
            {
                leadingWhitespace = lines[0].Substring(0, i);
                break;
            }
        }

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith(leadingWhitespace))
            {
                lines[i] = lines[i].Substring(leadingWhitespace.Length);
            }
        }

        return lines;
    }

    private static string? ParseDocCommentXml(string? xml)
    {
        // XML doc comments come wrapped in <member ...> ... </member>
        // Remove this root, and strip leading whitespace from the children.
        // Alternatively, if the doc XML was invalid, 'xml' just contains a top-level comment

        if (string.IsNullOrWhiteSpace(xml))
            return null;

        using (var sr = new StringReader(xml))
        using (var reader = XmlReader.Create(sr))
        {
            reader.Read();
            if (reader.NodeType == XmlNodeType.Element && reader.Name == "member")
            {
                reader.MoveToContent();
                return reader.ReadInnerXml().Trim('\n');
            }
            else
            {
                return null;
            }
        }
    }

    private (OnPropertyNameChangedInfo? onPropertyNameChanged, OnPropertyNameChangedInfo? onPropertyNameChanging) FindOnPropertyNameChangedMethod(
        INamedTypeSymbol typeSymbol,
        IPropertySymbol property) =>
            (this.propertyChangedInterfaceAnalyser!.FindOnPropertyNameChangedMethod(typeSymbol, property.Name, property.Type, property.ContainingType),
            this.propertyChangingInterfaceAnalyser!.FindOnPropertyNameChangedMethod(typeSymbol, property.Name, property.Type, property.ContainingType));


}
