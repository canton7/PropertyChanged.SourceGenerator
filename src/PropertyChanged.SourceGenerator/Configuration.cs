using System;
using System.Collections.Generic;
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
    public string[] RaisePropertyChangedMethodNames { get; set; } = new[] {
        "OnPropertyChanged", "RaisePropertyChanged", "NotifyOfPropertyChange", "NotifyPropertyChanged", 
    };

    public string[] RaisePropertyChangingMethodNames { get; set; } = new[] {
        "OnPropertyChanging", "RaisePropertyChanging", "NotifyOfPropertyChanging", "NotifyPropertyChanging",
    };

    public string[] RemovePrefixes { get; set; } = new[] { "_" };
    public string[] RemoveSuffixes { get; set; } = Array.Empty<string>();
    public string? AddPrefix { get; set; } = null;
    public string? AddSuffix { get; set; } = null;
    public Capitalisation FirstLetterCapitalisation { get; set; } = Capitalisation.Uppercase; 
}
