﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PropertyChanged.SourceGenerator.Analysis
{
    public partial class Analyser
    {
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
                    this.ResolveAutoDependsOn(typeAnalysis, propertySymbol, propertySymbol, lookups);
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
                    // This must be the name of a property we're generating
                    if (dependsOn != null && lookups.TryGet(dependsOn, out var dependsOnMember))
                    {
                        // If we're generating this property, make sure we use the generated name...
                        if (lookups.TryGet(member, out var memberAnalysis))
                        {
                            dependsOnMember.AddAlsoNotify(AlsoNotifyMember.FromMemberAnalysis(memberAnalysis));
                        }
                        else if (member is IPropertySymbol property)
                        {
                            dependsOnMember.AddAlsoNotify(AlsoNotifyMember.FromProperty(
                                property,
                                this.FindOnPropertyNameChangedMethod(typeAnalysis.TypeSymbol, property)));
                        }
                        else
                        {
                            // If we're not generating anything from this, and it's not a property, raise
                            this.diagnostics.RaiseDependsOnAppliedToFieldWithoutNotify(attribute, member);
                        }
                    }
                    else
                    {
                        this.diagnostics.RaiseDependsOnPropertyDoesNotExist(dependsOn, attribute, member);
                    }
                }
            }
        }

        /// <param name="notifyProperty">The property to raise an event for</param>
        /// <param name="analyseProperty">The property whose body should be analysed</param>
        private void ResolveAutoDependsOn(TypeAnalysis typeAnalysis, IPropertySymbol notifyProperty, IPropertySymbol analyseProperty, TypeAnalysisLookups lookups)
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
                        // Is it another property, which might itself have a body we need to analyse?
                        this.ResolveAutoDependsOn(typeAnalysis, notifyProperty, dependsOn, lookups);
                    }
                }
            }
        }
    }
}
