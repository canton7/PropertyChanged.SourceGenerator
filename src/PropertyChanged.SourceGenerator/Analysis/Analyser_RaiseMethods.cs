using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis
{
    public partial class Analyser
    {
        private bool TryFindPropertyRaiseMethod(
            INamedTypeSymbol typeSymbol,
            TypeAnalysis typeAnalysis,
            IReadOnlyList<TypeAnalysis> baseTypeAnalyses)
        {
            // Try and find out how we raise the PropertyChanged event
            // 1. If noone's defined the PropertyChanged event yet, we'll define it ourselves
            // 2. Otherwise, try and find a method to raise the event:
            //   a. If PropertyChanged is in a base class, we'll need to abort if we can't find one
            //   b. If PropertyChanged is in our class, we'll just define one and call it

            // They might have defined the event but not the interface, so we'll just look for the event by its
            // signature
            var eventSymbol = TypeAndBaseTypes(typeSymbol)
                .SelectMany(x => x.GetMembers("PropertyChanged"))
                .OfType<IEventSymbol>()
                .FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.Type, this.propertyChangedEventHandlerSymbol) &&
                    !x.IsStatic);

            // If there's no event, the base type in our hierarchy is defining it
            typeAnalysis.RequiresEvent = eventSymbol == null && baseTypeAnalyses.Count == 0;

            // Try and find a method with a name we recognise and a signature we know how to call
            // We prioritise the method name over things like the signature or where in the type hierarchy
            // it is. One we've found any method with a name we're looking for, stop: it's most likely they've 
            // just messed up the signature
            RaisePropertyChangedMethodSignature? signature = null;
            string? methodName = null;
            foreach (string name in this.config.RaisePropertyChangedMethodNames)
            {
                var methods = TypeAndBaseTypes(typeSymbol)
                    .SelectMany(x => x.GetMembers(name))
                    .OfType<IMethodSymbol>()
                    .Where(x => !x.IsOverride && !x.IsStatic)
                    .ToList();
                if (methods.Count > 0)
                {
                    signature = FindCallableOverload(methods);
                    if (signature != null)
                    {
                        methodName = name;
                    }
                    else
                    {
                        this.diagnostics.ReportCouldNotFindCallableRaisePropertyChangedOverload(typeSymbol, name);
                        return false;
                    }
                    break;
                }
            }

            if (signature != null)
            {
                // We found a method which we know how to call
                typeAnalysis.RequiresRaisePropertyChangedMethod = false;
                typeAnalysis.RaisePropertyChangedMethodName = methodName!;
                typeAnalysis.RaisePropertyChangedMethodSignature = signature.Value;
            }
            else
            {
                // The base type in our hierarchy is defining its own
                // Make sure that that type can actually access the event, if it's pre-existing
                if (eventSymbol != null && baseTypeAnalyses.Count == 0 &&
                    !SymbolEqualityComparer.Default.Equals(eventSymbol.ContainingType, typeSymbol))
                {
                    this.diagnostics.ReportCouldNotFindRaisePropertyChangedMethod(typeSymbol);
                    return false;
                }

                typeAnalysis.RequiresRaisePropertyChangedMethod = baseTypeAnalyses.Count == 0;
                typeAnalysis.RaisePropertyChangedMethodName = this.config.RaisePropertyChangedMethodNames[0];
                typeAnalysis.RaisePropertyChangedMethodSignature = RaisePropertyChangedMethodSignature.Default;
            }

            return true;

            RaisePropertyChangedMethodSignature? FindCallableOverload(List<IMethodSymbol> methods)
            {
                // We care about the order in which we choose an overload, which unfortunately means we're quadratic
                if (methods.Any(x => IsAccessibleNormalInstanceMethod(x) &&
                    x.Parameters.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, this.propertyChangedEventArgsSymbol) &&
                    IsNormalParameter(x.Parameters[0])))
                {
                    return new RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType.PropertyChangedEventArgs, hasOldAndNew: false);
                }

                if (methods.Any(x => IsAccessibleNormalInstanceMethod(x) &&
                    x.Parameters.Length == 1 &&
                    x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                    IsNormalParameter(x.Parameters[0])))
                {
                    return new RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType.String, hasOldAndNew: false);
                }

                if (methods.Any(x => IsAccessibleNormalInstanceMethod(x) &&
                    x.Parameters.Length == 3 &&
                    SymbolEqualityComparer.Default.Equals(x.Parameters[0].Type, this.propertyChangedEventArgsSymbol) &&
                    IsNormalParameter(x.Parameters[0]) &&
                    x.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
                    IsNormalParameter(x.Parameters[1]) &&
                    x.Parameters[2].Type.SpecialType == SpecialType.System_Object &&
                    IsNormalParameter(x.Parameters[2])))
                {
                    return new RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType.PropertyChangedEventArgs, hasOldAndNew: true);
                }
                if (methods.Any(x => IsAccessibleNormalInstanceMethod(x) &&
                    x.Parameters.Length == 3 &&
                    x.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                    IsNormalParameter(x.Parameters[0]) &&
                    x.Parameters[1].Type.SpecialType == SpecialType.System_Object &&
                    IsNormalParameter(x.Parameters[1]) &&
                    x.Parameters[2].Type.SpecialType == SpecialType.System_Object &&
                    IsNormalParameter(x.Parameters[2])))
                {
                    return new RaisePropertyChangedMethodSignature(RaisePropertyChangedNameType.String, hasOldAndNew: true);
                }

                return null;
            }

            bool IsAccessibleNormalInstanceMethod(IMethodSymbol method) =>
                !method.IsGenericMethod &&
                method.ReturnsVoid &&
                this.compilation.IsSymbolAccessibleWithin(method, typeSymbol);

            bool IsNormalParameter(IParameterSymbol parameter) =>
                parameter.RefKind == RefKind.None;
        }
    }
}
