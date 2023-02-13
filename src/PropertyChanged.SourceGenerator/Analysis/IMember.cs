using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis;

public interface IMember
{
    [MemberNotNullWhen(true, nameof(FullyQualifiedTypeName))]
    [MemberNotNullWhen(true, nameof(Name))]
    string? FullyQualifiedTypeName { get; }
    string? Name { get; }

    OnPropertyNameChangedInfo? OnPropertyNameChanged { get; }
    OnPropertyNameChangedInfo? OnPropertyNameChanging { get; }
}
