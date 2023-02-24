using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace PropertyChanged.SourceGenerator;

public enum Capitalisation
{
    None,
    Uppercase,
    Lowercase,
}

public class Configuration
{
    public ImmutableArray<string> RaisePropertyChangedMethodNames { get; set; } = ImmutableArray.Create(
        "OnPropertyChanged", "RaisePropertyChanged", "NotifyOfPropertyChange", "NotifyPropertyChanged"
    );

    public ImmutableArray<string> RaisePropertyChangingMethodNames { get; set; } = ImmutableArray.Create(
        "OnPropertyChanging", "RaisePropertyChanging", "NotifyOfPropertyChanging", "NotifyPropertyChanging"
    );

    public ImmutableArray<string> RemovePrefixes { get; set; } = ImmutableArray.Create("_");
    public ImmutableArray<string> RemoveSuffixes { get; set; } = ImmutableArray<string>.Empty;
    public string? AddPrefix { get; set; } = null;
    public string? AddSuffix { get; set; } = null;
    public Capitalisation FirstLetterCapitalisation { get; set; } = Capitalisation.Uppercase;
    public bool EnableAutoNotify { get; set; } = true;
}
