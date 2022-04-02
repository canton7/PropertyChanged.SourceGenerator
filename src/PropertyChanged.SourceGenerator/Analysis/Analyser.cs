using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PropertyChanged.SourceGenerator.Analysis;

public partial class Analyser
{
    private readonly DiagnosticReporter diagnostics;
    private readonly Compilation compilation;
    private readonly ConfigurationParser configurationParser;
    private readonly INamedTypeSymbol? inpcSymbol;
    private readonly INamedTypeSymbol? propertyChangedEventHandlerSymbol;
    private readonly INamedTypeSymbol? propertyChangedEventArgsSymbol;
    private readonly INamedTypeSymbol notifyAttributeSymbol;
    private readonly INamedTypeSymbol alsoNotifyAttributeSymbol;
    private readonly INamedTypeSymbol dependsOnAttributeSymbol;
    private readonly INamedTypeSymbol isChangedAttributeSymbol;

    public Analyser(
        DiagnosticReporter diagnostics,
        Compilation compilation,
        ConfigurationParser configurationParser)
    {
        this.diagnostics = diagnostics;
        this.compilation = compilation;
        this.configurationParser = configurationParser;

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
            // Make sure it's not e.g. C<Foo>, rather C<T>
            Debug.Assert(SymbolEqualityComparer.Default.Equals(typeSymbol, typeSymbol.OriginalDefinition));

            // If we've already analysed this one, return
            if (results.ContainsKey(typeSymbol))
                return;

            // If we haven't analysed its base type yet, do that now. This will then happen recursively
            // Special System.Object, as we'll hit it a lot
            // Use OriginalDefinition here, in case the child inerits from e.g. Base<Foo>: we want to analyse
            // Base<T>
            if (typeSymbol.BaseType != null
                && typeSymbol.BaseType.SpecialType != SpecialType.System_Object
                && !results.ContainsKey(typeSymbol.BaseType.OriginalDefinition))
            {
                Analyse(typeSymbol.BaseType.OriginalDefinition);
            }

            // If we're not actually supposed to analyse this type, bail. We have to do this after the base
            // type analysis check above, as we can have TypeWeAnalyse depends on TypeWeDontAnalyse depends
            // on TypeWeAnalyse.
            if (!typeSymbols.Contains(typeSymbol))
                return;

            // Right, we know we've analysed all of the base types by now. Fetch them
            var baseTypes = new List<TypeAnalysis>();
            for (var t = typeSymbol.BaseType?.OriginalDefinition; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType?.OriginalDefinition)
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
        var typeAnalysis = new TypeAnalysis()
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

    private void AnalyseInner(TypeAnalysis typeAnalysis, List<TypeAnalysis> baseTypeAnalyses)
    {
        var typeSymbol = typeAnalysis.TypeSymbol;

        if (this.inpcSymbol == null)
            throw new InvalidOperationException();

        var config = this.configurationParser.Parse(typeSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree);

        if (!this.TryFindPropertyRaiseMethod(typeAnalysis, baseTypeAnalyses, config))
        {
            typeAnalysis.CanGenerate = false;
        }

        // If we've got any base types we're generating partial types for , that will have the INPC interface
        // and event on, for sure
        typeAnalysis.HasInpcInterface = baseTypeAnalyses.Any(x => x.CanGenerate)
            || typeSymbol.AllInterfaces.Contains(this.inpcSymbol, SymbolEqualityComparer.Default);
        typeAnalysis.NullableContext = this.compilation.Options.NullableContextOptions;

        this.ResoveInheritedIsChanged(typeAnalysis, baseTypeAnalyses);

        foreach (var member in typeSymbol.GetMembers())
        {
            MemberAnalysis? memberAnalysis = null;
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
        this.ResolveDependsOn(typeAnalysis);

        if (!IsPartial(typeSymbol))
        {
            typeAnalysis.CanGenerate = false;
            if (typeAnalysis.Members.Count > 0)
            {
                this.diagnostics.ReportTypeIsNotPartial(typeSymbol);
            }
        }

        for (var outerType = typeSymbol.ContainingType; outerType != null; outerType = outerType.ContainingType)
        {
            if (!IsPartial(outerType))
            {
                typeAnalysis.CanGenerate = false;
                if (typeAnalysis.Members.Count > 0)
                {
                    this.diagnostics.ReportOuterTypeIsNotPartial(outerType, typeSymbol);
                }
            }
        }

        bool IsPartial(INamedTypeSymbol type) =>
            type.DeclaringSyntaxReferences
                .Select(x => x.GetSyntax())
                .OfType<ClassDeclarationSyntax>()
                .Any(x => x.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
    }

    private MemberAnalysis? AnalyseField(IFieldSymbol field, AttributeData notifyAttribute, Configuration config)
    {
        if (field.IsReadOnly)
        {
            this.diagnostics.RaiseReadonlyBackingMember(field);
            return null;
        }
        var result = this.AnalyseMember(field, field.Type, notifyAttribute, config);
        return result;
    }

    private MemberAnalysis? AnalyseProperty(IPropertySymbol property, AttributeData notifyAttribute, Configuration config)
    {
        if (property.GetMethod == null || property.SetMethod == null)
        {
            this.diagnostics.RaiseBackingPropertyMustHaveGetterAndSetter(property);
            return null;
        }
        var result = this.AnalyseMember(property, property.Type, notifyAttribute, config);
        return result;
    }

    private MemberAnalysis? AnalyseMember(
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

        // We can't have a getter/setter being internal, and the setter/getter being protected
        if ((getterAccessibility == Accessibility.Internal && setterAccessibility == Accessibility.Protected) ||
            (getterAccessibility == Accessibility.Protected && setterAccessibility == Accessibility.Internal))
        {
            this.diagnostics.ReportIncomapatiblePropertyAccessibilities(type, notifyAttribute);
            getterAccessibility = Accessibility.ProtectedOrInternal;
            setterAccessibility = Accessibility.ProtectedOrInternal;
        }

        string name = explicitName ?? this.TransformName(backingMember, config);
        var result = new MemberAnalysis()
        {
            BackingMember = backingMember,
            Name = name,
            Type = type,
            GetterAccessibility = getterAccessibility,
            SetterAccessibility = setterAccessibility,
            OnPropertyNameChanged = this.FindOnPropertyNameChangedMethod(backingMember.ContainingType, name, type, backingMember.ContainingType),
            DocComment = ParseDocComment(backingMember.GetDocumentationCommentXml()),
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
}
