using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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

    private readonly Dictionary<INamedTypeSymbol, IEventSymbol> typeToInterfaceEventCache = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<DiscoveredMethodInfoKey, DiscoveredMethodInfo> discoveredMethodInfoCache = new();

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

        
        IEventSymbol? eventSymbol = null;

        bool updateTypeToInterfaceEventCache = false;
        // Keep a cache of base type -> implementation, as many viewmodels will share a common base type and
        // FindImplementationForInterfaceMember isn't cheap
        if (typeSymbol.BaseType?.SpecialType != SpecialType.System_Object)
        {
            updateTypeToInterfaceEventCache = !this.typeToInterfaceEventCache.TryGetValue(typeSymbol.BaseType!, out eventSymbol);
        }

        // Start by looking for the event by its implementation. This catches explicitly-implemented events as
        // well as being slightly cheaper.
        if (eventSymbol == null)
        {
            eventSymbol = (IEventSymbol?)typeSymbol.FindImplementationForInterfaceMember(this.interfaceEventSymbol);
        }

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

        if (updateTypeToInterfaceEventCache)
        {
            this.typeToInterfaceEventCache[typeSymbol.BaseType!] = eventSymbol;
        }

        bool isGeneratingAnyParent = baseTypeAnalyses.Any(x => x.CanGenerate);

        // If they haven't defined the event, they can't have a method to raise it, so we can skip this
        var discoveredMethodInfo = eventSymbol != null ? this.DiscoverMethods(typeSymbol, config) : DiscoveredMethodInfo.None;

        // Get this populated now -- we'll need to adjust our behaviour based on what we find
        var onAnyPropertyChangedOrChangingInfo = this.FindOnAnyPropertyChangedOrChangingMethod(typeSymbol, out var onAnyPropertyChangedMethod);

        bool canCallRaiseMethod = true;
        string? raiseMethodName;
        RaisePropertyChangedOrChangingMethodSignature raiseMethodSignature;
        var raiseMethodType = RaisePropertyChangedMethodType.None;

        if (discoveredMethodInfo.Signature != null)
        {
            // We found a method which we know how to call.

            // Users aren't supposed to define their own overrides of this method: they're supposed to define
            // a hook which we call. So if this method was defined on the type we're currently analysing,
            // raise a warning.
            if (SymbolEqualityComparer.Default.Equals(discoveredMethodInfo.Method!.ContainingType, typeSymbol))
            {
                if (discoveredMethodInfo.Method.IsOverride)
                {
                    this.ReportUserDefinedRaisePropertyChangedOrChangingMethodOverride(discoveredMethodInfo.Method);
                }
                raiseMethodType = RaisePropertyChangedMethodType.None;
            }
            else if (discoveredMethodInfo.Method.IsVirtual || discoveredMethodInfo.Method.IsOverride)
            {
                raiseMethodType = RaisePropertyChangedMethodType.Override;
            }
            else
            {
                this.ReportRaisePropertyChangedOrChangingMethodIsNonVirtual(discoveredMethodInfo.Method);
                if (onAnyPropertyChangedOrChangingInfo != null)
                {
                    this.ReportCannotCallOnAnyPropertyChangedOrChangingBecauseRaisePropertyChangedOrChangingIsNonVirtual(onAnyPropertyChangedMethod!, discoveredMethodInfo.Method.Name);
                }
                raiseMethodType = RaisePropertyChangedMethodType.None;
            }

            if (onAnyPropertyChangedOrChangingInfo is { } info &&
                ((info.HasOld && !discoveredMethodInfo.Signature.Value.HasOld) || (info.HasNew && !discoveredMethodInfo.Signature.Value.HasNew)))
            {
                this.ReportCannotPopulateOnAnyPropertyChangedOrChangingOldAndNew(onAnyPropertyChangedMethod!, discoveredMethodInfo.Method.Name);
            }

            raiseMethodName = discoveredMethodInfo.Method!.Name;
            raiseMethodSignature = discoveredMethodInfo.Signature.Value;
        }
        else
        {
            // We didn't find a method we recognise and know how to call, so we're generating our own

            // If we found one with the right name but which we didn't know how to call, raise a warning
            if (discoveredMethodInfo.MethodNamesFoundButDidntKnowHowToCall.Length > 0)
            {
                this.ReportCouldNotFindCallableRaisePropertyChangedOrChangingOverload(typeSymbol, discoveredMethodInfo.MethodNamesFoundButDidntKnowHowToCall[0]);
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
                if (discoveredMethodInfo.MethodNamesFoundButDidntKnowHowToCall.Length == 0)
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

        string GetMatchingName(InterfaceAnalysis theirAnalysis, ImmutableArray<string> theirNames, ImmutableArray<string> ourNames)
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

    private DiscoveredMethodInfo DiscoverMethods(INamedTypeSymbol typeSymbol, Configuration config)
    {
        // Walk down the type hierarchy until we find a method we recognise and know how to call, or we find cached info for a base type
        // we've already looked at.

        // Note that different types might have different configs, which might affect what names we look for in what order, so we need to
        // make sure that we key the cache on the config used.

        // We prefer the first name listed in methodNames, regardless of whether it appears earlier or later in the type hierarchy. This means
        // that we need to effectively walk the entire type hierarchy for each value in methodNames.
        // We could have a situation where our base type defines methodNames[0], and we define methodNames[1]. In this case, we need to use
        // the info for methodNames[0]
        // There isn't much point in caching every level of every type hierarchy, firstly because most types will inherit from a common base INPC class,
        // and secondly because as we analyse a derived type we'll stop probing once we discover one parent that we've cached, without looking further
        // up the type hierarchy.

        var methodNames = this.GetRaisePropertyChangedOrChangingEventNames(config);
        // TODO: Cache this?
        var methodNamesLookup = new HashSet<string>(methodNames);
        var methodNamesEquatableArray = methodNames.AsEquatableArray();

        // We shouldn't be called twice for the same method
        var key = new DiscoveredMethodInfoKey(typeSymbol, methodNamesEquatableArray);
        Debug.Assert(!this.discoveredMethodInfoCache.ContainsKey(key));

        // The most common case is that the type inherits from one that we already know about and which has the same config as us, and doesn't
        // add any new members that we care about, so optimise for this case.
        if (typeSymbol.BaseType != null && 
            this.discoveredMethodInfoCache.TryGetValue(new DiscoveredMethodInfoKey(typeSymbol.BaseType, methodNamesEquatableArray), out var discoveredCachedMethodInfo) &&
            !typeSymbol.GetMembers().Any(x => x is IMethodSymbol { IsStatic: false } && methodNamesLookup.Contains(x.Name)))
        {
            return discoveredCachedMethodInfo;
        }

        // Fetching GetMembers(name) a lot is expensive, as internally it fetches all and then filters.
        // Build up all a lookup of all members that we care about, to avoid calling .GetMembers() a lot.
        // Method name -> methods on any type with that name
        Dictionary<string, List<IMethodSymbol>>? membersLookup = null;

        // We'll keep an eye open for cached results on the way down the hierarchy. Stop when we find one, as that means we've previously examined
        // that part of the type hierarchy
        DiscoveredMethodInfo? discoveredBaseMethodInfo = null;
        bool first = true;
        foreach (var thisOrBaseTypeSymbol in TypeAndBaseTypes(typeSymbol))
        {
            // No point in looking up typeSymbol -- we don't expect to be inspecting the same type twice
            if (!first && this.discoveredMethodInfoCache.TryGetValue(new DiscoveredMethodInfoKey(thisOrBaseTypeSymbol, methodNamesEquatableArray), out var cacheItem))
            {
                discoveredBaseMethodInfo = cacheItem;
                // No point in going further
                break;
            }

            foreach (var member in thisOrBaseTypeSymbol.GetMembers())
            {
                if (member is IMethodSymbol method && !method.IsStatic && methodNamesLookup.Contains(member.Name))
                {
                    membersLookup ??= new();
                    if (!membersLookup.TryGetValue(method.Name, out var methodSymbolsForThisMethod))
                    {
                        methodSymbolsForThisMethod = new();
                        membersLookup[method.Name] = methodSymbolsForThisMethod;
                    }
                    methodSymbolsForThisMethod.Add(method);
                }
            }

            first = false;
        }

        // Try and find a method with a name we recognise and a signature we know how to call.
        // We prioritise, in order:
        // 1. The method name. Names earlier in the list are preferred, even if they have a worse signature or are further down in the class hierarchy
        // 2. The signature. Better signatures are always preferred
        // 3. Where in the class hierarchy it is
        // We prioritise the method name over things like the signature or where in the type hierarchy it is.
        // We'll look for methods all the way down the class hierarchy, but we'll only complain if we find one
        // with an unknown signature on a class which implements INPC.

        // Don't even bother if membersLookup is null, which means that we haven't found any members with the right names on this type or its parents
        DiscoveredMethodInfo? discoveredMethodInfo = null;
        var methodNamesFoundButDidntKnowHowToCall = discoveredBaseMethodInfo?.MethodNamesFoundButDidntKnowHowToCall ?? ImmutableArray<string>.Empty;
        if (membersLookup != null)
        {
            foreach (string methodName in methodNames)
            {
                if (methodName == discoveredBaseMethodInfo?.Method?.Name && RaisePropertyChangedOrChangingMethodSignatureBetternessComparer.Instance.Compare(discoveredBaseMethodInfo?.Signature, discoveredMethodInfo?.Signature) > 0)
                {
                    discoveredMethodInfo = discoveredBaseMethodInfo;
                }

                // We don't filter on IsOverride. That means if there is an override, we'll pick it up before the
                // base type. This matters, because we check whether we found an override further down.
                if (membersLookup.TryGetValue(methodName, out var methods))
                {
                    foreach (var discoveredMethod in methods)
                    {
                        if (this.TryClassifyRaisePropertyChangedOrChangingMethod(discoveredMethod, typeSymbol) is { } signature)
                        {
                            if (RaisePropertyChangedOrChangingMethodSignatureBetternessComparer.Instance.Compare(signature, discoveredMethodInfo?.Signature) > 0)
                            {
                                discoveredMethodInfo = new(discoveredMethod, signature, methodNamesFoundButDidntKnowHowToCall);
                            }
                        }
                        else
                        {
                            if (discoveredMethod.ContainingType.AllInterfaces.Contains(this.interfaceSymbol, SymbolEqualityComparer.Default))
                            {
                                methodNamesFoundButDidntKnowHowToCall = methodNamesFoundButDidntKnowHowToCall.Add(methodName);
                                discoveredMethodInfo = new(null, null, methodNamesFoundButDidntKnowHowToCall);
                            }
                        }
                    }

                    if (discoveredMethodInfo != null)
                    {
                        // We found something for this method name. Stop now.
                        break;
                    }
                }
            }
        }
        else
        {
            discoveredMethodInfo = discoveredBaseMethodInfo;
        }

        if (discoveredMethodInfo == null)
        {
            discoveredMethodInfo = new(null, null, methodNamesFoundButDidntKnowHowToCall);
        }

        // Cache this against this type, and all parent types between us and the type which defines this method
        foreach (var type in TypeAndBaseTypes(typeSymbol))
        {
            this.discoveredMethodInfoCache[new(type, methodNamesEquatableArray)] = discoveredMethodInfo!.Value;

            if (SymbolEqualityComparer.Default.Equals(discoveredMethodInfo?.Method?.ContainingType, type))
            {
                break;
            }
        }

        return discoveredMethodInfo.Value;
    }

    protected abstract bool ShouldGenerateIfInterfaceNotPresent();

    protected abstract ImmutableArray<string> GetRaisePropertyChangedOrChangingEventNames(Configuration config);

    protected abstract RaisePropertyChangedOrChangingMethodSignature? TryClassifyRaisePropertyChangedOrChangingMethod(IMethodSymbol method, INamedTypeSymbol typeSymbol);

    protected abstract OnPropertyNameChangedInfo? FindOnAnyPropertyChangedOrChangingMethod(INamedTypeSymbol typeSymbol, out IMethodSymbol? method);

    protected abstract void ReportCouldNotFindRaisePropertyChangingOrChangedMethod(INamedTypeSymbol typeSymbol, string eventName);

    protected abstract void ReportCouldNotFindCallableRaisePropertyChangedOrChangingOverload(INamedTypeSymbol typeSymbol, string name);

    protected abstract void ReportUserDefinedRaisePropertyChangedOrChangingMethodOverride(IMethodSymbol method);

    protected abstract void ReportCannotCallOnAnyPropertyChangedOrChangingBecauseRaisePropertyChangedOrChangingIsNonVirtual(IMethodSymbol method, string raisePropertyChangedMethodName);

    protected abstract void ReportCannotPopulateOnAnyPropertyChangedOrChangingOldAndNew(IMethodSymbol method, string raisePropertyChangedMethodName);

    protected abstract void ReportRaisePropertyChangedOrChangingMethodIsNonVirtual(IMethodSymbol method);

    private readonly record struct DiscoveredMethodInfoKey(INamedTypeSymbol TypeSymbol, EquatableArray<string> RaisePropertyChangedOrChangingEventNames)
    {
        public bool Equals(DiscoveredMethodInfoKey other)
        {
            return SymbolEqualityComparer.Default.Equals(this.TypeSymbol, other.TypeSymbol) &&
                this.RaisePropertyChangedOrChangingEventNames.Equals(other.RaisePropertyChangedOrChangingEventNames);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            unchecked
            {
                hash = hash * 23 + SymbolEqualityComparer.Default.GetHashCode(this.TypeSymbol);
                hash = hash * 23 + this.RaisePropertyChangedOrChangingEventNames.GetHashCode();
            }
            return hash;
        }
    }

    private readonly record struct DiscoveredMethodInfo(IMethodSymbol? Method, RaisePropertyChangedOrChangingMethodSignature? Signature, ImmutableArray<string> MethodNamesFoundButDidntKnowHowToCall)
    {
        public static DiscoveredMethodInfo None { get; } = new(null, null, ImmutableArray<string>.Empty);
    }

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
