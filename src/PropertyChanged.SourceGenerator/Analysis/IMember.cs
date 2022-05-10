using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis;

public interface IMember
{
    [MemberNotNullWhen(true, nameof(Type))]
    [MemberNotNullWhen(true, nameof(Name))]
    bool IsCallable { get; }
    ITypeSymbol? Type { get; }
    string? Name { get; }

    OnPropertyNameChangedInfo? OnPropertyNameChanged { get; }
    OnPropertyNameChangedInfo? OnPropertyNameChanging { get; }
}
