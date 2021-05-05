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

            context.RegisterForPostInitialization(ctx => ctx.AddSource("Attributes", Generator.FileHeader + @"

namespace PropertyChanged.SourceGenerator
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Field | global::System.AttributeTargets.Property, AllowMultiple = false)]
    internal class NotifyAttribute : global::System.Attribute
    {
    }
}"));
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not SyntaxContextReceiver receiver)
                return;

            var config = new Configuration();
            var diagnostics = new DiagnosticReporter(context);
            var analyser = new Analyser(diagnostics, config, context.Compilation);

            // If we've got diagnostics here, bail
            if (diagnostics.HasDiagnostics)
                return;

            var analyses = receiver.Types.Select(x => analyser.Analyse(x)).ToList();

            foreach (var analysis in analyses)
            {
                var generator = new Generator();
                generator.Generate(analysis);
                context.AddSource(analysis.TypeSymbol.Name, SourceText.From(generator.ToString(), Encoding.UTF8));
            }
        }
    }
}
