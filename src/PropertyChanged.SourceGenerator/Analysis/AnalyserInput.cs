using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis;

public struct AnalyserInput : IEquatable<AnalyserInput>
{
    public INamedTypeSymbol TypeSymbol { get; }
    // Member -> Attributes
    public Dictionary<ISymbol, List<AttributeData>> Attributes { get; } = new(SymbolEqualityComparer.Default);

    public AnalyserInput(INamedTypeSymbol typeSymbol)
    {
        this.TypeSymbol = typeSymbol.OriginalDefinition;
    }

    public void Update(ISymbol member, ImmutableArray<AttributeData> attributes)
    {
        if (!this.Attributes.TryGetValue(member, out var existingAttributes))
        {
            existingAttributes = new();
            this.Attributes.Add(member, existingAttributes);
        }
        // Avoid the box
        foreach (var attribute in attributes)
        {
            existingAttributes.Add(attribute);
        }
    }

    public override bool Equals(object obj) => obj is AnalyserInput other && this.Equals(other);

    public bool Equals(AnalyserInput other)
    {
        return SymbolEqualityComparer.Default.Equals(this.TypeSymbol, other.TypeSymbol);
    }

    public override int GetHashCode()
    {
        return SymbolEqualityComparer.Default.GetHashCode(this.TypeSymbol);
    }
}