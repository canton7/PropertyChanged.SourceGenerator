using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using PropertyChanged.SourceGenerator.Analysis;
using PropertyChanged.SourceGenerator.Pipeline;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;

namespace PropertyChanged.SourceGenerator;

[Generator]
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
        //var typesSource = context.SyntaxProvider.CreateSyntaxProvider(
        //    static (n, _) => n is FieldDeclarationSyntax or PropertyDeclarationSyntax,
        //    this.SyntaxNodeToTypeHierarchy);

        context.RegisterPostInitializationOutput(ctx => ctx.AddSource("Attributes", StringConstants.Attributes));

        // Collect all types which contain a field/property decorated with NotifyAttribute.
        // These will never be cached! That's OK: we'll generate a model in the next step which can be.
        // There will probably be duplicate symbols in here.
        var attributeContainingTypeSources = attributeNames.Select(attribute =>
        {
            return context.SyntaxProvider.ForAttributeWithMetadataName(
                attribute,
                // TODO: More filtering on VariableDeclaratorSyntax
                static (node, _) => node is VariableDeclaratorSyntax or PropertyDeclarationSyntax,
                static (ctx, token) => (type: ctx.TargetSymbol.ContainingType, compilation: ctx.SemanticModel.Compilation));
        }).ToList();

        var typesSource = Collect(attributeContainingTypeSources);

        var nullableContextAndConfigurationParser = context.CompilationProvider.Select(static (compilation, _) => compilation.Options.NullableContextOptions)
            .Combine(context.AnalyzerConfigOptionsProvider.Select(static (options, _) => new ConfigurationParser(options)));

        var modelsAndDiagnosticsSource = nullableContextAndConfigurationParser.Combine(typesSource).Select((input, token) =>
        {
            var (nullableContextAndConfigurationParser, inputTypesAndCompilation) = input;
            var (nullableContext, configurationParser) = nullableContextAndConfigurationParser;
            if (inputTypesAndCompilation.Length == 0)
            {
                // TODO: Cachable
                return (analyses: ReadOnlyEquatableList<TypeAnalysis>.Empty, diagnostics: ReadOnlyEquatableList<Diagnostic>.Empty);
            }

            var diagnostics = new DiagnosticReporter();
            var compilation = inputTypesAndCompilation[0].compilation;
            var analyzer = new Analyser(diagnostics, compilation, nullableContext, configurationParser);
            // TODO: We should be able to remove this HashSet and just use the Dictionary in analyzer.Analyze
            var types = new HashSet<INamedTypeSymbol>(inputTypesAndCompilation.Select(x => x.type), SymbolEqualityComparer.Default);

            var analyses = analyzer.Analyse(types);
            return (analyses: new ReadOnlyEquatableList<TypeAnalysis>(analyses.ToList()), diagnostics: new ReadOnlyEquatableList<Diagnostic>(diagnostics.Diagnostics));
        });

        // TODO: Make diagnostics cachable?
        var diagnosticsSource = modelsAndDiagnosticsSource.SelectMany((pair, token) => pair.diagnostics);

        context.RegisterSourceOutput(diagnosticsSource, (ctx, diagnostic) =>
        {
            ctx.ReportDiagnostic(diagnostic);
        });

        var analysesSource = modelsAndDiagnosticsSource.Select((pair, token) => pair.analyses);

        var eventArgsCacheSource = analysesSource.Select((typeAnalyses, token) =>
        {
            return Generator.CreateEventArgsCache(typeAnalyses);
        });

        var analysisAndEventArgsCacheSource = analysesSource
            .SelectMany((analysis, token) => analysis)
            .Combine(eventArgsCacheSource);

        context.RegisterSourceOutput(analysisAndEventArgsCacheSource, (ctx, pair) =>
        {
            var (typeAnalysis, eventArgsCache) = pair;
            if (typeAnalysis.CanGenerate)
            {
                var generator = new Generator(eventArgsCache);
                generator.Generate(typeAnalysis);
                ctx.AddSource(typeAnalysis.TypeSymbol.ToDisplayString(SymbolDisplayFormats.GeneratedFileName) + ".g", generator.ToString());
            }
        });

        context.RegisterSourceOutput(eventArgsCacheSource, (ctx, eventArgsCache) =>
        {
            if (!eventArgsCache.IsEmpty)
            {
                var generator = new Generator(eventArgsCache);
                generator.GenerateNameCache();
                ctx.AddSource("PropertyChanged.SourceGenerator.Internal.EventArgsCache.g", generator.ToString());
            }
        });

        //typesSource.Combine(compilationInfo).Select((input, token) =>
        //{

        //});
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

    //private INamedTypeSymbol? SyntaxNodeToTypeHierarchy(GeneratorAttributeSyntaxContext ctx, CancellationToken token)
    //{
    //    switch (ctx)
    //    {
    //        case FieldDeclarationSyntax fieldDeclaration:
    //        {
    //            foreach (var node in fieldDeclaration.Declaration.Variables)
    //            {
    //                if (GetContainingTypeIfHasAttribute(node) is { } type)
    //                {
    //                    return type;
    //                }
    //            }
    //            break;
    //        }
    //        case PropertyDeclarationSyntax propertyDeclaration:
    //        {
    //            if (GetContainingTypeIfHasAttribute(propertyDeclaration) is { } type)
    //            {
    //                return type;
    //            }
    //            break;
    //        }
    //    }

    //    return null;

    //    INamedTypeSymbol? GetContainingTypeIfHasAttribute(SyntaxNode node)
    //    {
    //        if (ctx.SemanticModel.GetDeclaredSymbol(node, token) is { } symbol &&
    //                symbol.GetAttributes().Any(x =>
    //                    x.AttributeClass?.ContainingNamespace.ToDisplayString() == "PropertyChanged.SourceGenerator"))
    //        {
    //            return symbol.ContainingType;
    //        }

    //        return null;
    //    }
    //}

    //public void Initialize(GeneratorInitializationContext context)
    //{
    //    context.RegisterForSyntaxNotifications(() => new SyntaxContextReceiver());

    //    context.RegisterForPostInitialization(ctx => ctx.AddSource("Attributes", StringConstants.Attributes));
    //}

    //public void Execute(GeneratorExecutionContext context)
    //{
    //    if (context.SyntaxContextReceiver is not SyntaxContextReceiver receiver)
    //        return;

    //    var diagnostics = new DiagnosticReporter();
    //    var configurationParser = new ConfigurationParser(context.AnalyzerConfigOptions, diagnostics);
    //    var fileNames = new HashSet<string>();

    //    try
    //    {
    //        var analyser = new Analyser(diagnostics, context.Compilation, configurationParser);

    //        // If we've got diagnostics here, bail
    //        if (diagnostics.HasDiagnostics)
    //            return;

    //        var analyses = analyser.Analyse(receiver.Types);

    //        var eventArgsCache = new EventArgsCache();
    //        foreach (var analysis in analyses)
    //        {
    //            if (analysis.CanGenerate)
    //            {
    //                var generator = new Generator(eventArgsCache);
    //                generator.Generate(analysis!);
    //                AddSource(analysis!.TypeSymbol.Name, generator.ToString());
    //            }
    //        }

    //        if (!eventArgsCache.IsEmpty)
    //        {
    //            var nameCacheGenerator = new Generator(eventArgsCache);
    //            nameCacheGenerator.GenerateNameCache();
    //            AddSource(Generator.EventArgsCacheName, nameCacheGenerator.ToString());
    //        }
    //    }
    //    finally
    //    {
    //        foreach (var diagnostic in diagnostics.Diagnostics)
    //        {
    //            context.ReportDiagnostic(diagnostic);
    //        }
    //    }

    //    void AddSource(string hintName, string sourceText)
    //    {
    //        string fileName = hintName;
    //        if (!fileNames.Add(fileName))
    //        {
    //            for (int i = 2; ; i++)
    //            {
    //                fileName = hintName + i;
    //                if (fileNames.Add(fileName))
    //                {
    //                    break;
    //                }
    //            }
    //        }

    //        context.AddSource(fileName + ".g", SourceText.From(sourceText, Encoding.UTF8));
    //    }
    //}
}
