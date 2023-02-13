using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis;

public partial class Analyser
{
    public void ResoveInheritedIsChanged(TypeAnalysisBuilder typeAnalysis, List<TypeAnalysisBuilder> baseTypeAnalyses)
    {
        // Copy the parent's, if it's accessible
        if (baseTypeAnalyses.FirstOrDefault() is { IsChangedSetterIsPrivate: false } directParent)
        {
            typeAnalysis.IsChangedPropertyName = directParent.IsChangedPropertyName;
            typeAnalysis.IsChangedSetterIsPrivate = false;
        }
    }

    public void ResolveIsChangedMember(
        TypeAnalysisBuilder typeAnalysis,
        ISymbol member,
        MemberAnalysisBuilder? memberAnalysis)
    {
        if (member.GetAttributes().FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, this.isChangedAttributeSymbol)) is { } attribute &&
            GetMemberType(member) is { } memberType)
        {
            // TODO: Think about if a derived class has an IsChanged property which shadows a base class?
            // We'll warn that we'll use the base one, but that's not true.
            // TODO: Think about overridden IsChanged properties?
            if (typeAnalysis.IsChangedPropertyName != null)
            {
                this.diagnostics.ReportMultipleIsChangedProperties(typeAnalysis.IsChangedPropertyName, attribute, member);
            }
            else if (memberType.SpecialType != SpecialType.System_Boolean)
            {
                this.diagnostics.ReportNonBooleanIsChangedProperty(member);
            }
            else if (memberAnalysis == null && member is IPropertySymbol { SetMethod: null })
            {
                this.diagnostics.ReportIsChangedDoesNotHaveSetter(member);
            }
            else
            {
                // If it's got [Notify] on it, use the generated property name
                if (memberAnalysis != null)
                {
                    typeAnalysis.IsChangedPropertyName = memberAnalysis.Name;
                    typeAnalysis.IsChangedSetterIsPrivate = memberAnalysis.SetterAccessibility == Accessibility.Private;
                }
                else
                {
                    typeAnalysis.IsChangedPropertyName = member.Name;
                    typeAnalysis.IsChangedSetterIsPrivate = member is IPropertySymbol { SetMethod.DeclaredAccessibility: Accessibility.Private };
                }
            }
        }
    }
}
