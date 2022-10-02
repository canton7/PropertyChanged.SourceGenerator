using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static PropertyChanged.SourceGenerator.Analysis.Utils;

namespace PropertyChanged.SourceGenerator.Analysis;

public partial class Analyser
{
    private static readonly ImmutableHashSet<IPropertySymbol> emptyPropertyHashSet = ImmutableHashSet<IPropertySymbol>.Empty.WithComparer(SymbolEqualityComparer.Default);

    private void ResolveDependsOn(TypeAnalysis typeAnalysis)
    {
        var lookups = new TypeAnalysisLookups(typeAnalysis);
        foreach (var member in typeAnalysis.TypeSymbol.GetMembers().Where(x => x is IFieldSymbol or IPropertySymbol))
        {
            var dependsOnAttributes = member.GetAttributes()
                .Where(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, this.dependsOnAttributeSymbol))
                .ToList();
            if (dependsOnAttributes.Count > 0)
            {
                this.ResolveManualDependsOn(typeAnalysis, lookups, member, dependsOnAttributes);
            }
            else if (member is IPropertySymbol propertySymbol)
            {
                this.ResolveAutoDependsOn(typeAnalysis, propertySymbol, propertySymbol, lookups, emptyPropertyHashSet);
            }
        }
    }

    private void ResolveManualDependsOn(TypeAnalysis typeAnalysis, TypeAnalysisLookups lookups, ISymbol member, List<AttributeData> dependsOnAttributes)
    {
        foreach (var attribute in dependsOnAttributes)
        {
            var dependsOnValues = ExtractAttributeStringParams(attribute);
            foreach (string? dependsOn in dependsOnValues)
            {
                if (string.IsNullOrWhiteSpace(dependsOn))
                    continue;

                AlsoNotifyMember? alsoNotifyMember = null;
                // If we're generating this property, make sure we use the generated name...
                if (lookups.TryGet(member, out var memberAnalysis))
                {
                    alsoNotifyMember = AlsoNotifyMember.FromMemberAnalysis(memberAnalysis);
                }
                else if (member is IPropertySymbol property)
                {
                    alsoNotifyMember = AlsoNotifyMember.FromProperty(property,
                        this.FindOnPropertyNameChangedMethod(typeAnalysis.TypeSymbol, property));
                }
                else
                {
                    // If we're not generating anything from this, and it's not a property, raise
                    this.diagnostics.RaiseDependsOnAppliedToFieldWithoutNotify(attribute, member);
                }

                if (alsoNotifyMember != null)
                {
                    if (lookups.TryGet(dependsOn!, out var dependsOnMember))
                    {
                        // Is this the name of a property we're generating on this type?
                        dependsOnMember.AddAlsoNotify(alsoNotifyMember.Value);
                    }
                    else
                    {
                        // We'll assume it'll pass through RaisePropertyChanged
                        if (typeAnalysis.INotifyPropertyChanged.CanCallRaiseMethod &&
                            typeAnalysis.INotifyPropertyChanged.RaiseMethodType == RaisePropertyChangedMethodType.None)
                        {
                            this.diagnostics.ReportDependsOnSpecifiedButRaisePropertyChangedMethodCannotBeOverridden(attribute, member, dependsOn!, typeAnalysis.INotifyPropertyChanged.RaiseMethodName!);
                        }
                        if (typeAnalysis.INotifyPropertyChanging.CanCallRaiseMethod &&
                            typeAnalysis.INotifyPropertyChanging.RaiseMethodType == RaisePropertyChangedMethodType.None)
                        {
                            this.diagnostics.ReportDependsOnSpecifiedButRaisePropertyChangingMethodCannotBeOverridden(attribute, member, dependsOn!, typeAnalysis.INotifyPropertyChanged.RaiseMethodName!);
                        }
                        typeAnalysis.AddDependsOn(dependsOn!, alsoNotifyMember.Value);
                    }
                }
            }
        }
    }

    /// <param name="notifyProperty">The property to raise an event for</param>
    /// <param name="analyseProperty">The property whose body should be analysed</param>
    private void ResolveAutoDependsOn(
        TypeAnalysis typeAnalysis,
        IPropertySymbol notifyProperty,
        IPropertySymbol analyseProperty,
        TypeAnalysisLookups lookups,
        ImmutableHashSet<IPropertySymbol> visitedProperties)
    {
        if (analyseProperty.GetMethod?.Locations.FirstOrDefault() is { } location &&
            location.SourceTree?.GetRoot()?.FindNode(location.SourceSpan) is { } getterNode)
        {
            // Annoyingly, we're looking for references to properties which don't actually exist yet
            // We'll therefore have to do this entirely by name. We're looking for IdentifierNames which we
            // recognise.
            // We're looking for accesses which aren't part of a MemberAccessExpression ('Bar'), or are and the
            // left side is 'this' ('this.Bar').
            // Once we've got this, discount:
            // - pre/post increment/decrement, as this isn't a get
            // - Assignment, as this isn't a get
            foreach (var node in getterNode.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            {
                var parent = node.Parent;
                // If this is the RHS of a member access (this.Bar or foo.Bar)
                if (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node)
                {
                    if (!memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression) ||
                        !memberAccess.Expression.IsKind(SyntaxKind.ThisExpression))
                        continue;
                    parent = memberAccess.Parent;
                }

                switch (parent?.Kind())
                {
                    case SyntaxKind.PreIncrementExpression:
                    case SyntaxKind.PostIncrementExpression:
                    case SyntaxKind.PreDecrementExpression:
                    case SyntaxKind.PostDecrementExpression:
                        continue;
                }

                if (parent is AssignmentExpressionSyntax)
                    continue;

                if (lookups.TryGet(node.Identifier.ValueText, out var memberAnalysis))
                {
                    // It's probably a property access. Is it something we've analysed already?
                    memberAnalysis.AddAlsoNotify(AlsoNotifyMember.FromProperty(
                        notifyProperty,
                        this.FindOnPropertyNameChangedMethod(typeAnalysis.TypeSymbol, notifyProperty)));
                }
                else if (typeAnalysis.TypeSymbol.GetMembers().OfType<IPropertySymbol>()
                    .FirstOrDefault(x => x.Name == node.Identifier.ValueText) is { } dependsOn)
                {
                    // Is it another property defined on the same type, which might itself have a body
                    // we need to analyse?
                    // Avoid infinite recursion if a property refers to itself, or two properties refer to each other.
                    if (!visitedProperties.Contains(dependsOn))
                    {
                        this.ResolveAutoDependsOn(typeAnalysis, notifyProperty, dependsOn, lookups, visitedProperties.Add(dependsOn));
                    }
                }
                else if (TypeAndBaseTypes(typeAnalysis.TypeSymbol.BaseType!).SelectMany(x => x.GetMembers().OfType<IPropertySymbol>())
                    .FirstOrDefault(x => x.Name == node.Identifier.ValueText) is { } baseProperty)
                {
                    // Is it another property defined on a base type? We'll need to stick it in
                    // the RaisePropertyChanged method
                    typeAnalysis.AddDependsOn(baseProperty.Name, AlsoNotifyMember.FromProperty(
                        notifyProperty,
                        this.FindOnPropertyNameChangedMethod(typeAnalysis.TypeSymbol, notifyProperty)));
                }
            }
        }
    }
}
