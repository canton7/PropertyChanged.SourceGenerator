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
    internal enum Getter
    {
        Public = 6,
        ProtectedInternal = 5,
        Internal = 4,
        Protected = 3,
        PrivateProtected = 2,
        Private = 1,
    }

    internal enum Setter
    {
        Public = 6,
        ProtectedInternal = 5,
        Internal = 4,
        Protected = 3,
        PrivateProtected = 2,
        Private = 1,
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Field | global::System.AttributeTargets.Property, AllowMultiple = false)]
    internal class NotifyAttribute : global::System.Attribute
    {
        public NotifyAttribute() { }
        public NotifyAttribute(string name, Getter get = Getter.Public, Setter set = Setter.Public) { }
        public NotifyAttribute(Getter get, Setter set = Setter.Public) { }
        public NotifyAttribute(Setter set) { }
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Field | global::System.AttributeTargets.Property, AllowMultiple = true)]
    internal class AlsoNotifyAttribute : global::System.Attribute
    {
        public AlsoNotifyAttribute(params string[] otherProperties) { }
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

            var analyses = analyser.Analyse(receiver.Types);

            var eventArgsCache = new EventArgsCache();
            foreach (var analysis in analyses)
            {
                var generator = new Generator(eventArgsCache);
                generator.Generate(analysis!);
                context.AddSource(analysis!.TypeSymbol.Name, SourceText.From(generator.ToString(), Encoding.UTF8));
            }

            var nameCacheGenerator = new Generator(eventArgsCache);
            nameCacheGenerator.GenerateNameCache();
            context.AddSource("PropertyChangedEventArgsCache", SourceText.From(nameCacheGenerator.ToString(), Encoding.UTF8));
        }
    }
}
