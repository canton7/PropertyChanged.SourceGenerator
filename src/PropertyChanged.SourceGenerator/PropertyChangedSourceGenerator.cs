using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PropertyChanged.SourceGenerator.Analysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PropertyChanged.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public class PropertyChangedSourceGenerator : IIncrementalGenerator
{
    private static readonly string[] attributeNames = new[]
    {
        "PropertyChanged.SourceGenerator.NotifyAttribute",
        "PropertyChanged.SourceGenerator.AlsoNotifyAttribute",
        "PropertyChanged.SourceGenerator.DependsOnAttribute",
        "PropertyChanged.SourceGenerator.IsChangedAttribute",
        "PropertyChanged.SourceGenerator.PropertyAttributeAttribute",
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource("PropertyChanged.SourceGenerator.Attributes", StringConstants.Attributes));

        // Collect all types which contain a field/property decorated with NotifyAttribute.
        // These will never be cached! That's OK: we'll generate a model in the next step which can be.
        // There will probably be duplicate symbols in here.
        var attributeContainingTypeSources = attributeNames.Select(attribute =>
        {
            return context.SyntaxProvider.ForAttributeWithMetadataName(
                attribute,
                (node, token) => node is VariableDeclaratorSyntax
                {
                    Parent: VariableDeclarationSyntax
                    {
                        Parent: FieldDeclarationSyntax
                        {
                            AttributeLists.Count: > 0
                        }
                    }
                } || node is PropertyDeclarationSyntax
                {
                    AttributeLists.Count: > 0,
                },
                (ctx, token) => (member: ctx.TargetSymbol, attributes: ctx.Attributes, compilation: ctx.SemanticModel.Compilation))
            .WithComparer(AlwaysFalseEqualityComparer<(ISymbol member, ImmutableArray<AttributeData> attributes, Compilation compilation)>.Instance)
            .WithTrackingName($"attributeContainingTypeSources_{attribute}");
        }).ToList();

        var typesSource = Collect(attributeContainingTypeSources)
            .WithComparer(AlwaysFalseEqualityComparer<ImmutableArray<(ISymbol member, ImmutableArray<AttributeData> attributes, Compilation compilation)>>.Instance)
            .WithTrackingName("typesSource");

        var nullableContextAndConfigurationParser = context.CompilationProvider.Select((compilation, _) => compilation.Options.NullableContextOptions)
            .Combine(context.AnalyzerConfigOptionsProvider.Select((options, _) => new ConfigurationParser(options)))
            .WithTrackingName("nullableContextAndConfigurationParser");

        var modelsAndDiagnosticsSource = nullableContextAndConfigurationParser.Combine(typesSource).Select((input, token) =>
        {
            var (nullableContextAndConfigurationParser, inputTypesAndCompilation) = input;
            var (nullableContext, configurationParser) = nullableContextAndConfigurationParser;
            if (inputTypesAndCompilation.Length == 0)
            {
                return (analyses: EquatableArray<TypeAnalysis>.Empty, diagnostics: EquatableArray<Diagnostic>.Empty);
            }

            var analyserInputs = new Dictionary<INamedTypeSymbol, AnalyserInput>(SymbolEqualityComparer.Default);
            foreach (var (member, attributes, _) in inputTypesAndCompilation)
            {
                if (!analyserInputs.TryGetValue(member.ContainingType, out var existingInputs))
                {
                    existingInputs = new(member.ContainingType);
                    analyserInputs.Add(member.ContainingType, existingInputs);
                }
                existingInputs.Update(member, attributes);
            }

            var diagnostics = new DiagnosticReporter();

            var compilation = inputTypesAndCompilation[0].compilation;
            var analyzer = new Analyser(diagnostics, compilation, nullableContext, configurationParser);

            var analyses = analyzer.Analyse(analyserInputs, token);
            // These are going to be inputs to SelectMany, which will convert them to ImmutableArrays anyway
            return (analyses: analyses.ToImmutableArray().AsEquatableArray(), diagnostics: diagnostics.GetDiagnostics());
        }).WithTrackingName("modelsAndDiagnosticsSource");

        var diagnosticsSource = modelsAndDiagnosticsSource.SelectMany((pair, token) => pair.diagnostics.AsImmutableArray())
            .WithTrackingName("diagnosticsSource");

        context.RegisterSourceOutput(diagnosticsSource, (ctx, diagnostic) =>
        {
            ctx.ReportDiagnostic(diagnostic);
        });

        var analysesSource = modelsAndDiagnosticsSource.Select((pair, token) => pair.analyses)
            .WithTrackingName("analysesSource");

        var eventArgsCacheAndLookupSource = analysesSource.Select((typeAnalyses, token) =>
        {
            return Generator.CreateEventArgsCacheAndLookup(typeAnalyses);
        }).WithTrackingName("eventArgsCacheAndLookupSource");

        var eventArgsCacheSource = eventArgsCacheAndLookupSource.Select((x, token) => x.cache)
            .WithTrackingName("eventArgsCacheSource");

        var eventArgsCacheLookupSource = eventArgsCacheAndLookupSource.Select((x, token) => x.lookup)
            .WithTrackingName("eventArgsCacheLookupSource");

        var analysisAndEventArgsCacheLookupSource = analysesSource
            .SelectMany((analysis, token) => analysis.AsImmutableArray())
            .Combine(eventArgsCacheLookupSource)
            .WithTrackingName("analysisAndEventArgsCacheLookupSource");

        context.RegisterSourceOutput(analysisAndEventArgsCacheLookupSource, (ctx, pair) =>
        {
            var (typeAnalysis, eventArgsCacheLookup) = pair;
            var generator = new Generator(eventArgsCacheLookup);
            generator.Generate(typeAnalysis);
            ctx.AddSource(typeAnalysis.TypeNameForGeneratedFileName + ".g", generator.ToSourceText());
        });

        context.RegisterSourceOutput(eventArgsCacheSource, (ctx, eventArgsCache) =>
        {
            if (!eventArgsCache.IsEmpty)
            {
                var generator = new EventArgsCacheGenerator(eventArgsCache);
                generator.GenerateNameCache();
                ctx.AddSource("PropertyChanged.SourceGenerator.Internal.EventArgsCache.g", generator.ToSourceText());
            }
        });
    }

    private static IncrementalValueProvider<ImmutableArray<T>> Collect<T>(List<IncrementalValuesProvider<T>> sources)
    {
        var aggregate = sources[0].Collect();
        for (int i = 1; i < sources.Count; i++)
        {
            aggregate = aggregate.Combine(sources[i].Collect()).Select((pair, token) => pair.Left.AddRange(pair.Right));
        }
        return aggregate;
    }
}
