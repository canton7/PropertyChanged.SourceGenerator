using Microsoft.CodeAnalysis;

namespace PropertyChanged.SourceGenerator.Analysis;

public class InterfaceAnalysis
{
    /// <summary>
    /// True if we need to add this interface to the partial class's list
    /// </summary>
    public bool RequiresInterface { get; set; }

    /// <summary>
    /// True if we need to add this event to the partial class
    /// </summary>
    public bool RequiresEvent { get; set; }

    /// <summary>
    /// True if this method can be called, because either we're generating it, or because it's user-defined and accessible
    /// </summary>
    public bool CanCallRaiseMethod { get; set; } 

    /// <summary>
    /// The name of the event: PropertyChanged or PropertyChanging
    /// </summary>
    public string EventName { get; set; } = null!;

    /// <summary>
    /// The name of the EventArgs: PropertyChangedEventArgs or PropertyChangingEventArgs
    /// </summary>
    public INamedTypeSymbol EventArgsSymbol { get; set; } = null!;

    /// <summary>
    /// The fully-qualified type name of the EventArgs: global::System.ComponentModel.PropertyChangedEventArgs etc
    /// </summary>
    public string EventArgsFullyQualifiedTypeName { get; set; } = null!;

    /// <summary>
    /// The type of method we're generating, or None if we're not generating it
    /// </summary>
    public RaisePropertyChangedMethodType RaiseMethodType { get; set; }

    /// <summary>
    /// The name of the method to raise this event
    /// </summary>
    public string? RaiseMethodName { get; set; }

    /// <summary>
    /// The signature of the raise method
    /// </summary>
    public RaisePropertyChangedOrChangingMethodSignature RaiseMethodSignature { get; set; }

    /// <summary>
    /// The signature of the 'OnAnyPropertyChanged' method, if any
    /// </summary>
    public OnPropertyNameChangedInfo? OnAnyPropertyChangedOrChangingInfo { get; set; }
}
