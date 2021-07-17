using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using PropertyChanged.SourceGenerator.Analysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PropertyChanged.SourceGenerator
{
    [Generator]
    public class PropertyChangedSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxContextReceiver());

            context.RegisterForPostInitialization(ctx => ctx.AddSource("Attributes", StringConstants.Attributes));
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not SyntaxContextReceiver receiver)
                return;

            var diagnostics = new DiagnosticReporter();
            var configurationParser = new ConfigurationParser(context.AnalyzerConfigOptions, diagnostics);
            try
            {
                var analyser = new Analyser(diagnostics, context.Compilation, configurationParser);

                // If we've got diagnostics here, bail
                if (diagnostics.HasDiagnostics)
                    return;

                var analyses = analyser.Analyse(receiver.Types);

                var eventArgsCache = new EventArgsCache();
                foreach (var analysis in analyses)
                {
                    if (analysis.CanGenerate)
                    {
                        var generator = new Generator(eventArgsCache);
                        generator.Generate(analysis!);
                        context.AddSource(analysis!.TypeSymbol.Name, SourceText.From(generator.ToString(), Encoding.UTF8));
                    }
                }

                if (!eventArgsCache.IsEmpty)
                {
                    var nameCacheGenerator = new Generator(eventArgsCache);
                    nameCacheGenerator.GenerateNameCache();
                    context.AddSource("PropertyChangedEventArgsCache", SourceText.From(nameCacheGenerator.ToString(), Encoding.UTF8));
                }
            }
            finally
            {
                foreach (var diagnostic in diagnostics.Diagnostics)
                {
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
