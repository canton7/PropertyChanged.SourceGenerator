using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PropertyChanged.SourceGenerator.Analysis
{
    public partial class Analyser
    {
        private void ResolveAlsoNotify(TypeAnalysis typeAnalysis, List<TypeAnalysis> baseTypeAnalyses)
        {
            // We've already warned if there are AlsoNotify attributes on members that we haven't analysed
            foreach (var member in typeAnalysis.Members)
            {
                var alsoNotifyAttributes = member.BackingMember.GetAttributes().Where(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, this.alsoNotifyAttributeSymbol));
                foreach (var attribute in alsoNotifyAttributes)
                {
                    var alsoNotifyValues = ExtractAttributeStringParams(attribute);

                    // We only allow them to use the property name. If we didn't, consider:
                    // 1. Derived class has the same property name as base class (shadowed)
                    // 2. Derived class has the same field name as a base class (generating different properties)
                    // 3. Derived class has a field with the same name as a base class' property
                    // 4. Derived class has a property with the same name as a base class' field

                    foreach (string? alsoNotify in alsoNotifyValues)
                    {
                        // Remember that we're probably, but not necessarily, notifying a property which we're also
                        // generating.
                        // Allow null and emptystring as special cases
                        if (alsoNotify == member.Name)
                        {
                            this.diagnostics.ReportAlsoNotifyForSelf(alsoNotify, attribute, member.BackingMember);
                        }
                        else
                        {
                            ITypeSymbol? foundCallableType = null;
                            if (!string.IsNullOrEmpty(alsoNotify))
                            {
                                var foundType = baseTypeAnalyses.Prepend(typeAnalysis)
                                    .SelectMany(x => x.Members)
                                    .FirstOrDefault(x => x.Name == alsoNotify)?
                                    .Type;
                                foundType ??= TypeAndBaseTypes(typeAnalysis.TypeSymbol)
                                    .SelectMany(x => x.GetMembers(alsoNotify!))
                                    .OfType<IPropertySymbol>()
                                    .FirstOrDefault(x => this.compilation.IsSymbolAccessibleWithin(x, typeAnalysis.TypeSymbol))?
                                    .Type;
                                foundCallableType = foundType;
                                // Also let them use "Item[]", if an indexer exists
                                if (foundType == null && alsoNotify!.EndsWith("[]"))
                                {
                                    string indexerName = alsoNotify.Substring(0, alsoNotify.Length - "[]".Length);
                                    foundType = TypeAndBaseTypes(typeAnalysis.TypeSymbol)
                                        .SelectMany(x => x.GetMembers("this[]"))
                                        .OfType<IPropertySymbol>()
                                        .FirstOrDefault(x => x.IsIndexer && x.MetadataName == indexerName)?
                                        .Type;
                                }
                                if (foundType == null)
                                {
                                    this.diagnostics.ReportAlsoNotifyPropertyDoesNotExist(alsoNotify!, attribute, member.BackingMember);
                                }
                            }
                            member.AddAlsoNotify(new AlsoNotifyMember(alsoNotify, foundCallableType));
                        }
                    }
                }
            }
        }
    }
}
