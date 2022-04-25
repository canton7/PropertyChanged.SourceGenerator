using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using static PropertyChanged.SourceGenerator.Analysis.Utils;

namespace PropertyChanged.SourceGenerator.Analysis;

public abstract class InterfaceAnalyser
{
    private readonly INamedTypeSymbol interfaceSymbol;
    private readonly INamedTypeSymbol eventHandlerSymbol;
    protected readonly INamedTypeSymbol EventArgsSymbol;
    private readonly string eventName;
    protected readonly DiagnosticReporter Diagnostics;
    protected readonly Compilation Compilation;

    protected InterfaceAnalyser(
        INamedTypeSymbol interfaceSymbol,
        INamedTypeSymbol eventHandlerSymbol,
        INamedTypeSymbol eventArgsSymbol,
        string eventName,
        DiagnosticReporter diagnostics,
        Compilation compilation)
    {
        this.interfaceSymbol = interfaceSymbol;
        this.eventHandlerSymbol = eventHandlerSymbol;
        this.EventArgsSymbol = eventArgsSymbol;
        this.eventName = eventName;
        this.Diagnostics = diagnostics;
        this.Compilation = compilation;
    }

    public void PopulateInterfaceAnalysis(
        INamedTypeSymbol typeSymbol,
        InterfaceAnalysis interfaceAnalysis,
        IReadOnlyList<TypeAnalysis> baseTypeAnalyses,
        Configuration config)
    {
        // If we've got any base types we're generating partial types for, that will have the INPC interface
        // and event on, for sure
        interfaceAnalysis.RequiresInterface = !baseTypeAnalyses.Any(x => x.CanGenerate)
            && !typeSymbol.AllInterfaces.Contains(this.interfaceSymbol, SymbolEqualityComparer.Default);

        // Try and find out how we raise the PropertyChanged event
        // 1. If noone's defined the PropertyChanged event yet, we'll define it ourselves
        // 2. Otherwise, try and find a method to raise the event:
        //   a. If PropertyChanged is in a base class, we'll need to abort if we can't find one
        //   b. If PropertyChanged is in our class, we'll just define one and call it

        // They might have defined the event but not the interface, so we'll just look for the event by its
        // signature
        var eventSymbol = TypeAndBaseTypes(typeSymbol)
            .SelectMany(x => x.GetMembers(this.eventName))
            .OfType<IEventSymbol>()
            .FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.Type, this.eventHandlerSymbol) &&
                !x.IsStatic);

        bool isGeneratingAnyParent = baseTypeAnalyses.Any(x => x.CanGenerate);

        // If there's no event, the base type in our hierarchy is defining it
        interfaceAnalysis.RequiresEvent = eventSymbol == null && !isGeneratingAnyParent;

        // Try and find a method with a name we recognise and a signature we know how to call
        // We prioritise the method name over things like the signature or where in the type hierarchy
        // it is. One we've found any method with a name we're looking for, stop: it's most likely they've 
        // just messed up the signature
        RaisePropertyChangedMethodSignature? signature = null;
        IMethodSymbol? method = null;
        foreach (string name in this.GetRaisePropertyChangedOrChangingEventNames(config))
        {
            // We don't filter on IsOverride. That means if there is an override, we'll pick it up before the
            // base type. This matters, because we check whether we found an override further down.
            var methods = TypeAndBaseTypes(typeSymbol)
                .SelectMany(x => x.GetMembers(name))
                .OfType<IMethodSymbol>()
                .Where(x => !x.IsStatic)
                .ToList();
            if (methods.Count > 0)
            {
                if (!this.TryFindCallableOverload(methods, out method, out signature, typeSymbol))
                {
                    interfaceAnalysis.CanCallRaiseMethod = false;
                    this.Diagnostics.ReportCouldNotFindCallableRaisePropertyChangedOverload(typeSymbol, name);
                }
                break;
            }
        }

        // Get this populated now -- we'll need to adjust our behaviour based on what we find
        this.FindOnAnyPropertyChangedOrChangingMethod(typeSymbol, interfaceAnalysis, out var onAnyPropertyChangedMethod);

        if (signature != null)
        {
            // We found a method which we know how to call.

            // Users aren't supposed to define their own overrides of this method: they're supposed to define
            // a hook which we call. So if this method was defined on the type we're currently analysing,
            // raise a warning.
            if (SymbolEqualityComparer.Default.Equals(method!.ContainingType, typeSymbol))
            {
                if (method.IsOverride)
                {
                    this.Diagnostics.ReportUserDefinedRaisePropertyChangedMethodOverride(method);
                }
                interfaceAnalysis.RaiseMethodType = RaisePropertyChangedMethodType.None;
            }
            else if (method.IsVirtual || method.IsOverride)
            {
                interfaceAnalysis.RaiseMethodType = RaisePropertyChangedMethodType.Override;
            }
            else
            {
                this.Diagnostics.ReportRaisePropertyMethodIsNonVirtual(method);
                if (interfaceAnalysis.OnAnyPropertyChangedInfo != null)
                {
                    this.Diagnostics.ReportCannotCallOnAnyPropertyChangedBecauseRaisePropertyChangedIsNonVirtual(onAnyPropertyChangedMethod!, method.Name);
                }
                interfaceAnalysis.RaiseMethodType = RaisePropertyChangedMethodType.None;
            }

            if (interfaceAnalysis.OnAnyPropertyChangedInfo?.Signature == OnPropertyNameChangedSignature.OldAndNew &&
                !signature.Value.HasOldAndNew)
            {
                this.Diagnostics.ReportCannotPopulateOnAnyPropertyChangedOldAndNew(onAnyPropertyChangedMethod!, method.Name);
            }

            interfaceAnalysis.RaiseMethodName = method!.Name;
            interfaceAnalysis.RaiseMethodSignature = signature.Value;
        }
        else if (interfaceAnalysis.CanCallRaiseMethod)
        {
            // The base type in our hierarchy is defining its own
            // Make sure that that type can actually access the event, if it's pre-existing
            if (eventSymbol != null && !isGeneratingAnyParent &&
                !SymbolEqualityComparer.Default.Equals(eventSymbol.ContainingType, typeSymbol))
            {
                interfaceAnalysis.CanCallRaiseMethod = false;
                interfaceAnalysis.RaiseMethodType = RaisePropertyChangedMethodType.None;
                this.Diagnostics.ReportCouldNotFindRaisePropertyChangedMethod(typeSymbol);
            }
            else if (isGeneratingAnyParent)
            {
                interfaceAnalysis.RaiseMethodType = interfaceAnalysis.OnAnyPropertyChangedInfo == null
                    ? RaisePropertyChangedMethodType.None
                    : RaisePropertyChangedMethodType.Override;
            }
            else
            {
                interfaceAnalysis.RaiseMethodType = typeSymbol.IsSealed
                    ? RaisePropertyChangedMethodType.NonVirtual
                    : RaisePropertyChangedMethodType.Virtual;
            }

            interfaceAnalysis.RaiseMethodName = config.RaisePropertyChangedMethodNames[0];
            interfaceAnalysis.RaiseMethodSignature = new RaisePropertyChangedMethodSignature(
                RaisePropertyChangedNameType.PropertyChangedEventArgs,
                hasOldAndNew: interfaceAnalysis.OnAnyPropertyChangedInfo?.Signature == OnPropertyNameChangedSignature.OldAndNew,
                typeSymbol.IsSealed ? Accessibility.Private : Accessibility.Protected);
        }
    }

    protected abstract string[] GetRaisePropertyChangedOrChangingEventNames(Configuration config);

    protected abstract bool TryFindCallableOverload(List<IMethodSymbol> methods, out IMethodSymbol method, out RaisePropertyChangedMethodSignature? signature, INamedTypeSymbol typeSymbol);

    protected abstract void FindOnAnyPropertyChangedOrChangingMethod(INamedTypeSymbol typeSymbol, InterfaceAnalysis interfaceAnalysis, out IMethodSymbol? method);
}
