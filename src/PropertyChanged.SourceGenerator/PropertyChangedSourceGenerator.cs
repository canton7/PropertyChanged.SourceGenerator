using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using PropertyChanged.SourceGenerator.Analysis;
using PropertyChanged.SourceGenerator.Pipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace PropertyChanged.SourceGenerator;

[Generator]
public class PropertyChangedSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var typesSource = context.SyntaxProvider.CreateSyntaxProvider(
            static (n, _) => n is FieldDeclarationSyntax or PropertyDeclarationSyntax,
            this.SyntaxNodeToTypeHierarchy);

        var nullableContextOptions = context.CompilationProvider.Select(static (compilation, _) => compilation.Options.NullableContextOptions);

        var analyzerSource = context.CompilationProvider.Combine(context.AnalyzerConfigOptionsProvider).Select((tuple, token) =>
        {
            var (compilation, optionsProvider) = tuple;
            var configurationParser = new ConfigurationParser(optionsProvider);
            var analyzer = new Analyser(compilation, configurationParser);
            return analyzer;
        });

        typesSource.Combine(compilationInfo).Select((input, token) =>
        {

        });
    }

    private ReadOnlyEquatableList<INamedTypeSymbol> SyntaxNodeToTypeHierarchy(GeneratorSyntaxContext ctx, CancellationToken token)
    {
        switch (ctx.Node)
        {
            case FieldDeclarationSyntax fieldDeclaration:
            {
                foreach (var node in fieldDeclaration.Declaration.Variables)
                {
                    if (GetContainingTypeIfHasAttribute(node) is { } type)
                    {
                        return CreateHierarchy(type);
                    }
                }
                break;
            }
            case PropertyDeclarationSyntax propertyDeclaration:
            {
                if (GetContainingTypeIfHasAttribute(propertyDeclaration) is { } type)
                {
                    return CreateHierarchy(type);
                }
                break;
            }
        }

        return new ReadOnlyEquatableList<INamedTypeSymbol>(Array.Empty<INamedTypeSymbol>(), SymbolEqualityComparer.Default);

        INamedTypeSymbol? GetContainingTypeIfHasAttribute(SyntaxNode node)
        {
            if (ctx.SemanticModel.GetDeclaredSymbol(node, token) is { } symbol &&
                    symbol.GetAttributes().Any(x =>
                        x.AttributeClass?.ContainingNamespace.ToDisplayString() == "PropertyChanged.SourceGenerator"))
            {
                return symbol.ContainingType;
            }

            return null;
        }

        ReadOnlyEquatableList<INamedTypeSymbol> CreateHierarchy(INamedTypeSymbol type)
        {
            var typeHierarchy = new List<INamedTypeSymbol>();
            for (var t = type;
                t != null && SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, type.ContainingAssembly);
                t = t.BaseType)
            {
                typeHierarchy.Add(t);
            }
            return new ReadOnlyEquatableList<INamedTypeSymbol>(typeHierarchy, SymbolEqualityComparer.Default);
        }
    }

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
