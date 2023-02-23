using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static PropertyChanged.SourceGenerator.Analysis.Utils;

namespace PropertyChanged.SourceGenerator.Analysis;

public abstract class InterfaceAnalyser
{
    private readonly INamedTypeSymbol interfaceSymbol;

    private readonly string eventHandlerMetadataName;
    private INamedTypeSymbol? eventHandlerSymbolCache;
    private INamedTypeSymbol eventHandlerSymbol => this.eventHandlerSymbolCache ??= this.Compilation.GetTypeByMetadataName(this.eventHandlerMetadataName)!;

    protected readonly INamedTypeSymbol EventArgsSymbol;
    private readonly string eventArgsFullyQualifiedTypeName;
    private readonly string eventName;
    private readonly IEventSymbol interfaceEventSymbol;
    protected readonly DiagnosticReporter Diagnostics;
    protected readonly Compilation Compilation;
    private readonly Func<TypeAnalysisBuilder, InterfaceAnalysis> interfaceAnalysisGetter;

    protected InterfaceAnalyser(
        INamedTypeSymbol interfaceSymbol,
        string eventHandlerMetadataName,
        INamedTypeSymbol eventArgsSymbol,
        string eventName,
        DiagnosticReporter diagnostics,
        Compilation compilation,
        Func<TypeAnalysisBuilder, InterfaceAnalysis> interfaceAnalysisGetter)
    {
        this.interfaceSymbol = interfaceSymbol;
        this.eventHandlerMetadataName = eventHandlerMetadataName;
        this.EventArgsSymbol = eventArgsSymbol;
        this.eventArgsFullyQualifiedTypeName = eventArgsSymbol.ToDisplayString(SymbolDisplayFormats.FullyQualifiedTypeName);
        this.eventName = eventName;
        this.interfaceEventSymbol = this.interfaceSymbol.GetMembers(this.eventName).OfType<IEventSymbol>().First();
        this.Diagnostics = diagnostics;
        this.Compilation = compilation;
        this.interfaceAnalysisGetter = interfaceAnalysisGetter;
    }

    #region Interface Analysis

    public InterfaceAnalysis CreateInterfaceAnalysis(
        INamedTypeSymbol typeSymbol,
        IReadOnlyList<TypeAnalysisBuilder> baseTypeAnalyses,
        Configuration config)
    {
        bool hasInterface = typeSymbol.AllInterfaces.Contains(this.interfaceSymbol, SymbolEqualityComparer.Default);
        if (!this.ShouldGenerateIfInterfaceNotPresent() && !hasInterface)
        {
            return InterfaceAnalysis.Empty;
        }

        // Try and find out how we raise the PropertyChanged event
        // 1. If noone's defined the PropertyChanged event yet, we'll define it ourselves
        // 2. Otherwise, try and find a method to raise the event:
        //   a. If PropertyChanged is in a base class, we'll need to abort if we can't find one
        //   b. If PropertyChanged is in our class, we'll just define one and call it

        // Start by looking for the event by its implementation. This catches explicitly-implemented events as
        // well as being slightly cheaper.
        var eventSymbol = (IEventSymbol?)typeSymbol.FindImplementationForInterfaceMember(this.interfaceEventSymbol);

        // Fall back to searching for it by signature, as they might have defined the event but not the interface.
        // Do this for all types in the hierarchy. It's possible for them to define the event but not implement
        // the interface in a base type, and if they do that we need to not generate a duplicate event.
        if (eventSymbol == null)
        {
            eventSymbol = TypeAndBaseTypes(typeSymbol)
                .SelectMany(x => x.GetMembers(this.eventName))
                .OfType<IEventSymbol>()
                .FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.Type, this.eventHandlerSymbol) &&
                    !x.IsStatic);
        }

        bool isGeneratingAnyParent = baseTypeAnalyses.Any(x => x.CanGenerate);

        // TODO: This bit is massively inefficient. We could cache a lot between types in the hierarchy here.

        // Try and find a method with a name we recognise and a signature we know how to call.
        // We prioritise the method name over things like the signature or where in the type hierarchy
        // it is.
        // We'll look for methods all the way down the class hierarchy, but we'll only complain if we find one
        // with an unknown signature on a class which implements INPC.
        // We start at
        RaisePropertyChangedOrChangingMethodSignature? signature = null;
        IMethodSymbol? method = null;
        var methodNamesFoundButDidntKnowHowToCall = new List<string>();
        var methodNames = this.GetRaisePropertyChangedOrChangingEventNames(config);
        var allMethods = TypeAndBaseTypes(typeSymbol)
                .OfType<IMethodSymbol>()
                .Where(x => !x.IsStatic && methodNames.Contains(x.Name))
                .ToLookup(x => x.Name);
        foreach (string name in methodNames)
        {
            // We don't filter on IsOverride. That means if there is an override, we'll pick it up before the
            // base type. This matters, because we check whether we found an override further down.
            var methods = allMethods[name].ToList();
            if (methods.Count > 0)
            {
                // TryFindCallableRaisePropertyChangedOrChangingOverload mutates methods, so we need to check this now
                bool anyImplementsInpc = methods.Any(x => x.ContainingType.AllInterfaces.Contains(this.interfaceSymbol, SymbolEqualityComparer.Default));
                if (this.TryFindCallableRaisePropertyChangedOrChangingOverload(methods, out method, out signature, typeSymbol))
                {
                    break;
                }
                else if (anyImplementsInpc)
                {
                    methodNamesFoundButDidntKnowHowToCall.Add(name);
                    break;
                }
            }
        }

        // Get this populated now -- we'll need to adjust our behaviour based on what we find
        var onAnyPropertyChangedOrChangingInfo = this.FindOnAnyPropertyChangedOrChangingMethod(typeSymbol, out var onAnyPropertyChangedMethod);

        bool canCallRaiseMethod = true;
        string? raiseMethodName;
        RaisePropertyChangedOrChangingMethodSignature raiseMethodSignature;
        var raiseMethodType = RaisePropertyChangedMethodType.None;

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
                    this.ReportUserDefinedRaisePropertyChangedOrChangingMethodOverride(method);
                }
                raiseMethodType = RaisePropertyChangedMethodType.None;
            }
            else if (method.IsVirtual || method.IsOverride)
            {
                raiseMethodType = RaisePropertyChangedMethodType.Override;
            }
            else
            {
                this.ReportRaisePropertyChangedOrChangingMethodIsNonVirtual(method);
                if (onAnyPropertyChangedOrChangingInfo != null)
                {
                    this.ReportCannotCallOnAnyPropertyChangedOrChangingBecauseRaisePropertyChangedOrChangingIsNonVirtual(onAnyPropertyChangedMethod!, method.Name);
                }
                raiseMethodType = RaisePropertyChangedMethodType.None;
            }

            if (onAnyPropertyChangedOrChangingInfo is { } info &&
                ((info.HasOld && !signature.Value.HasOld) || (info.HasNew && !signature.Value.HasNew)))
            {
                this.ReportCannotPopulateOnAnyPropertyChangedOrChangingOldAndNew(onAnyPropertyChangedMethod!, method.Name);
            }

            raiseMethodName = method!.Name;
            raiseMethodSignature = signature.Value;
        }
        else
        {
            // We didn't find a method we recognise and know how to call, so we're generating our own

            // If we found one with the right name but which we didn't know how to call, raise a warning
            if (methodNamesFoundButDidntKnowHowToCall.Count > 0)
            {
                this.ReportCouldNotFindCallableRaisePropertyChangedOrChangingOverload(typeSymbol, methodNamesFoundButDidntKnowHowToCall[0]);
            }

            // Test whether the base type in our hierarchy is defining its own
            // Make sure that that type can actually access the event, if it's pre-existing. If it's explicitly implemented, we can't raise it
            if (eventSymbol != null && !isGeneratingAnyParent &&
                (!SymbolEqualityComparer.Default.Equals(eventSymbol.ContainingType, typeSymbol) ||
                eventSymbol.ExplicitInterfaceImplementations.Contains(this.interfaceEventSymbol, SymbolEqualityComparer.Default)))
            {
                canCallRaiseMethod = false;
                raiseMethodType = RaisePropertyChangedMethodType.None;

                // Don't raise this if we raised ReportCouldNotFindCallableRaisePropertyChangedOrChangingOverload above
                if (methodNamesFoundButDidntKnowHowToCall.Count == 0)
                {
                    this.ReportCouldNotFindRaisePropertyChangingOrChangedMethod(typeSymbol, eventSymbol.ToDisplayString(SymbolDisplayFormats.EventDefinition));
                }
            }
            else if (isGeneratingAnyParent)
            {
                raiseMethodType = onAnyPropertyChangedOrChangingInfo == null
                    ? RaisePropertyChangedMethodType.None
                    : RaisePropertyChangedMethodType.Override;
            }
            else
            {
                raiseMethodType = typeSymbol.IsSealed
                    ? RaisePropertyChangedMethodType.NonVirtual
                    : RaisePropertyChangedMethodType.Virtual;
            }

            // See if any of our base type analysis came up with a name.
            // If they didn't, we'll sort this out in PopulateRaiseMethodNameIfEmpty
            raiseMethodName =
                baseTypeAnalyses.Select(x => this.interfaceAnalysisGetter(x).RaiseMethodName).FirstOrDefault(x => x != null);

            raiseMethodSignature = new RaisePropertyChangedOrChangingMethodSignature(
                RaisePropertyChangedOrChangingNameType.PropertyChangedEventArgs,
                HasOld: onAnyPropertyChangedOrChangingInfo?.HasOld ?? false,
                HasNew: onAnyPropertyChangedOrChangingInfo?.HasNew ?? false,
                typeSymbol.IsSealed ? Accessibility.Private : Accessibility.Protected);
        }

        return new InterfaceAnalysis()
        {
            // If we've got any base types we're generating partial types for, that will have the INPC interface
            // and event on, for sure
            RequiresInterface = !hasInterface && !baseTypeAnalyses.Any(x => x.CanGenerate),
            // If there's no event, the base type in our hierarchy is defining it
            RequiresEvent = eventSymbol == null && !isGeneratingAnyParent,
            CanCallRaiseMethod = canCallRaiseMethod,
            EventName = this.eventName,
            EventArgsFullyQualifiedTypeName = this.eventArgsFullyQualifiedTypeName,
            RaiseMethodType = raiseMethodType,
            RaiseMethodName = raiseMethodName,
            RaiseMethodSignature = raiseMethodSignature,
            OnAnyPropertyChangedOrChangingInfo = onAnyPropertyChangedOrChangingInfo,
        };
    }

    public static void PopulateRaiseMethodNameIfEmpty(
        InterfaceAnalysis propertyChangedAnalysis,
        InterfaceAnalysis propertyChangingAnalysis,
        Configuration config)
    {
        if (propertyChangedAnalysis.CanCallRaiseMethod && propertyChangedAnalysis.RaiseMethodName == null)
        {
            // Do we have a name from PropertyChanging that we can copy?
            propertyChangedAnalysis.RaiseMethodName = GetMatchingName(propertyChangingAnalysis, config.RaisePropertyChangingMethodNames, config.RaisePropertyChangedMethodNames);
        }

        if (propertyChangingAnalysis.CanCallRaiseMethod && propertyChangingAnalysis.RaiseMethodName == null)
        {
            propertyChangingAnalysis.RaiseMethodName = GetMatchingName(propertyChangedAnalysis, config.RaisePropertyChangedMethodNames, config.RaisePropertyChangingMethodNames);
        }

        string GetMatchingName(InterfaceAnalysis theirAnalysis, string[] theirNames, string[] ourNames)
        {
            string? ourName = null;
            if (theirAnalysis.CanCallRaiseMethod && theirAnalysis.RaiseMethodName != null)
            {
                int index = theirNames.AsSpan().IndexOf(theirAnalysis.RaiseMethodName);
                if (index != -1)
                {
                    ourName = ourNames.ElementAtOrDefault(index);
                }
            }

            if (ourName == null)
            {
                ourName = ourNames[0];
            }

            return ourName;
        }
    }


    protected abstract bool ShouldGenerateIfInterfaceNotPresent();

    protected abstract string[] GetRaisePropertyChangedOrChangingEventNames(Configuration config);

    protected abstract bool TryFindCallableRaisePropertyChangedOrChangingOverload(List<IMethodSymbol> methods, out IMethodSymbol method, out RaisePropertyChangedOrChangingMethodSignature? signature, INamedTypeSymbol typeSymbol);

    protected abstract OnPropertyNameChangedInfo? FindOnAnyPropertyChangedOrChangingMethod(INamedTypeSymbol typeSymbol, out IMethodSymbol? method);

    protected abstract void ReportCouldNotFindRaisePropertyChangingOrChangedMethod(INamedTypeSymbol typeSymbol, string eventName);

    protected abstract void ReportCouldNotFindCallableRaisePropertyChangedOrChangingOverload(INamedTypeSymbol typeSymbol, string name);

    protected abstract void ReportUserDefinedRaisePropertyChangedOrChangingMethodOverride(IMethodSymbol method);

    protected abstract void ReportCannotCallOnAnyPropertyChangedOrChangingBecauseRaisePropertyChangedOrChangingIsNonVirtual(IMethodSymbol method, string raisePropertyChangedMethodName);

    protected abstract void ReportCannotPopulateOnAnyPropertyChangedOrChangingOldAndNew(IMethodSymbol method, string raisePropertyChangedMethodName);

    protected abstract void ReportRaisePropertyChangedOrChangingMethodIsNonVirtual(IMethodSymbol method);

    #endregion

    #region OnPropertyNameChanged / Changing

    /// <param name="typeSymbol">Type we're currently analysing</param>
    /// <param name="name">Name of the property to find an OnPropertyNameChanged method for</param>
    /// <param name="memberType">Type of the property</param>
    /// <param name="containingType">Type containing the property (may be a base type)</param>
    /// <returns></returns>
    public OnPropertyNameChangedInfo? FindOnPropertyNameChangedMethod(
        INamedTypeSymbol typeSymbol,
        string name,
        ITypeSymbol memberType,
        INamedTypeSymbol containingType)
    {
        string onChangedMethodName = this.GetOnPropertyNameChangedOrChangingMethodName(name);
        var methods = containingType.GetMembers(onChangedMethodName)
            .OfType<IMethodSymbol>()
            .Where(x => !x.IsOverride && !x.IsStatic)
            .ToList();

        OnPropertyNameChangedInfo? result = null;
        if (methods.Count > 0)
        {
            // FindCallableOverload might remove some...
            var firstMethod = methods[0];
            if ((result = this.FindCallableOnPropertyNameChangedOrChangingOverload(typeSymbol, methods, onChangedMethodName, memberType)) == null)
            {
                this.ReportInvalidOnPropertyNameChangedOrChangingSignature(name, onChangedMethodName, firstMethod);
            }
        }

        return result;
    }

    protected abstract string GetOnPropertyNameChangedOrChangingMethodName(string name);

    protected abstract OnPropertyNameChangedInfo? FindCallableOnPropertyNameChangedOrChangingOverload(
        INamedTypeSymbol typeSymbol,
        List<IMethodSymbol> methods,
        string onChangedMethodName,
        ITypeSymbol memberType);

    protected abstract void ReportInvalidOnPropertyNameChangedOrChangingSignature(string name, string onChangedMethodName, IMethodSymbol method);

    #endregion
}
