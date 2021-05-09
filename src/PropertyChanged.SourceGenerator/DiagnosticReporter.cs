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

        private static readonly DiagnosticDescriptor memberWithNameAlreadyExists = CreateDescriptor(
            "INPC003",
            "Member with this name already exists",
            "Attempted to generate property '{0}' for member '{1}', but a member with that name already exists. Skipping this property");
        public void ReportMemberWithNameAlreadyExists(ISymbol symbol, string name)
        {
            this.AddDiagnostic(memberWithNameAlreadyExists, symbol.Locations, name, symbol.Name);
        }

        private static readonly DiagnosticDescriptor anotherMemberHasSameGeneratedName = CreateDescriptor(
            "INPC004",
            "Another member has the same generated name as this one",
            "Member '{0}' will have the same generated property name '{1}' as member '{2}'. Skipping both properties");
        public void ReportAnotherMemberHasSameGeneratedName(ISymbol thisMember, ISymbol otherMember, string name)
        {
            this.AddDiagnostic(anotherMemberHasSameGeneratedName, thisMember.Locations, thisMember.Name, name, otherMember.Name);
        }

        private static readonly DiagnosticDescriptor incompatiblePropertyAccessibilities = CreateDescriptor(
            "INPC005",
            "Incompatible property accessibilities",
            "C# propertes may not have an internal getter and protected setter, or protected setter and internal getter. Defaulting both to protected internal");
        public void ReportIncomapatiblePropertyAccessibilities(ISymbol member, AttributeData notifyAttribute)
        {
            this.AddDiagnostic(incompatiblePropertyAccessibilities, AttributeLocations(notifyAttribute, member));
        }

        private static readonly DiagnosticDescriptor couldNotFindCallableRaisePropertyChangedOverload = CreateDescriptor(
            "INPC006",
            "Could not find callable method to raise PropertyChanged event",
            "Found one or more methods called '{0}' to raise the PropertyChanged event, but they had an unrecognised signatures or were inaccessible");
        public void RaiseCouldNotFindCallableRaisePropertyChangedOverload(INamedTypeSymbol typeSymbol, string name)
        {
            this.AddDiagnostic(couldNotFindCallableRaisePropertyChangedOverload, typeSymbol.Locations, name);
        }

        private static readonly DiagnosticDescriptor couldNotFindRaisePropertyChangedMethod = CreateDescriptor(
            "INPC007",
            "Could not find method to raise PropertyChanged event",
            "Could not find any suitable methods to raise the PropertyChanged event defined on a base class");
        public void RaiseCouldNotFindRaisePropertyChangedMethod(INamedTypeSymbol typeSymbol)
        {
            this.AddDiagnostic(couldNotFindRaisePropertyChangedMethod, typeSymbol.Locations);
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

        private static IEnumerable<Location> AttributeLocations(AttributeData? attributeData, ISymbol fallback)
        {
            // TODO: This squiggles the 'BasePath(...)' bit. Ideally we'd want '[BasePath(...)]' or perhaps just '...'.
            var attributeLocation = attributeData?.ApplicationSyntaxReference?.GetSyntax().GetLocation();
            return attributeLocation != null ? new[] { attributeLocation } : fallback.Locations;
        }
    }
}
