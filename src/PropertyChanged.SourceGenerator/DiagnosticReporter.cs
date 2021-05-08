using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator
{
    public class DiagnosticReporter
    {
        private readonly GeneratorExecutionContext context;

        public bool HasDiagnostics { get; private set; }
        public bool HasErrors { get; private set; }

        public DiagnosticReporter(GeneratorExecutionContext context)
        {
            this.context = context;
        }

        private static readonly DiagnosticDescriptor couldNotFindInpc = CreateDescriptor(
            "INPC001",
            "Unable to find INotifyPropertyChanged",
            "Unable to find the interface System.ComponentModel.INotifyPropertyChanged. Ensure you are referencing the containing assembly");
        public void ReportCouldNotFindInpc()
        {
            this.AddDiagnostic(couldNotFindInpc);
        }

        private static readonly DiagnosticDescriptor typeIsNotPartial = CreateDescriptor(
            "INPC002",
            "Type is not partial",
            "Type '{0}' must be partial in order for PropertyChanged.SourceGenerator to generate properties");
        public void ReportTypeIsNotPartial(INamedTypeSymbol typeSymbol)
        {
            this.AddDiagnostic(typeIsNotPartial, typeSymbol.Locations, typeSymbol.Name);
        }

        private static readonly DiagnosticDescriptor memberRenameResultedInConflict = CreateDescriptor(
            "INPC003",
            "Could not use name for property",
            "Attempted to generate property '{0}' for member '{1}', but a member with that name already exists. Skipping this property");
        public void ReportMemberRenameResultedInConflict(ISymbol symbol, string name)
        {
            this.AddDiagnostic(memberRenameResultedInConflict, symbol.Locations, name, symbol.Name);
        }

        private static DiagnosticDescriptor CreateDescriptor(string code, string title, string messageFormat, DiagnosticSeverity severity = DiagnosticSeverity.Warning)
        {
            string[] tags = severity == DiagnosticSeverity.Error ? new[] { WellKnownDiagnosticTags.NotConfigurable } : Array.Empty<string>();
            return new DiagnosticDescriptor(code, title, messageFormat, "PropertyChanged.SourceGenerator.Generation", severity, isEnabledByDefault: true, customTags: tags);
        }

        private void AddDiagnostic(DiagnosticDescriptor descriptor, IEnumerable<Location> locations, params object?[] args)
        {
            var locationsList = (locations as IReadOnlyList<Location>) ?? locations.ToList();
            this.AddDiagnostic(Diagnostic.Create(descriptor, locationsList.Count == 0 ? Location.None : locationsList[0], locationsList.Skip(1), args));
        }

        private void AddDiagnostic(DiagnosticDescriptor descriptor, Location? location = null, params object?[] args)
        {
            this.AddDiagnostic(Diagnostic.Create(descriptor, location ?? Location.None, args));
        }

        private void AddDiagnostic(Diagnostic diagnostic)
        {
            this.HasDiagnostics = true;
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                this.HasErrors = true;
            }
            this.context.ReportDiagnostic(diagnostic);
        }
    }
}
