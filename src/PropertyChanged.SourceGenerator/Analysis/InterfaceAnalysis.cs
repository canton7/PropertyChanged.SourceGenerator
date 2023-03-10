using System.Net.Cache;
using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis;

public record InterfaceAnalysis
{
    public static InterfaceAnalysis Empty { get; } = new()
    {
        RequiresInterface = false,
        RequiresEvent = false,
        CanCallRaiseMethod = false,
        EventName = "",
        EventArgsFullyQualifiedTypeName = "",
        RaiseMethodType = default,
        RaiseMethodName = null,
        RaiseMethodSignature = default,
        OnAnyPropertyChangedOrChangingInfo = null,
    };

    /// <summary>
    /// True if we need to add this interface to the partial class's list
    /// </summary>
    public required bool RequiresInterface { get; init; }

    /// <summary>
    /// True if we need to add this event to the partial class
    /// </summary>
    public required bool RequiresEvent { get; init; }

    /// <summary>
    /// True if this method can be called, because either we're generating it, or because it's user-defined and accessible
    /// </summary>
    public required bool CanCallRaiseMethod { get; init; } 

    /// <summary>
    /// The name of the event: PropertyChanged or PropertyChanging
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// The fully-qualified type name of the EventArgs: global::System.ComponentModel.PropertyChangedEventArgs etc
    /// </summary>
    public required string EventArgsFullyQualifiedTypeName { get; init; }

    /// <summary>
    /// The type of method we're generating, or None if we're not generating it
    /// </summary>
    public required RaisePropertyChangedMethodType RaiseMethodType { get; init; }

    /// <summary>
    /// The name of the method to raise this event
    /// </summary>
    // TODO: Make this immutable
    public required string? RaiseMethodName { get; init; }

    /// <summary>
    /// The signature of the raise method
    /// </summary>
    public required RaisePropertyChangedOrChangingMethodSignature RaiseMethodSignature { get; init; }

    /// <summary>
    /// The signature of the 'OnAnyPropertyChanged' method, if any
    /// </summary>
    public required OnPropertyNameChangedInfo? OnAnyPropertyChangedOrChangingInfo { get; init; }
}
