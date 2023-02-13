using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis;

public class TypeAnalysisLookups
{
    private readonly TypeAnalysisBuilder typeAnalysis;

    private Dictionary<string, MemberAnalysisBuilder>? nameLookup;
    private Dictionary<ISymbol, MemberAnalysisBuilder>? symbolLookup;

    public TypeAnalysisLookups(TypeAnalysisBuilder typeAnalysis) => this.typeAnalysis = typeAnalysis;

    public bool TryGet(string name, [NotNullWhen(true)] out MemberAnalysisBuilder? memberAnalysis)
    {
        this.nameLookup ??= this.typeAnalysis.Members.ToDictionary(x => x.Name, x => x, StringComparer.Ordinal);
        return this.nameLookup.TryGetValue(name, out memberAnalysis);
    }

    public bool TryGet(ISymbol symbol, [NotNullWhen(true)] out MemberAnalysisBuilder? memberAnalysis)
    {
        this.symbolLookup ??= this.typeAnalysis.Members.ToDictionary(x => x.BackingMember, x => x, SymbolEqualityComparer.Default);
        return this.symbolLookup.TryGetValue(symbol, out memberAnalysis);
    }
}
