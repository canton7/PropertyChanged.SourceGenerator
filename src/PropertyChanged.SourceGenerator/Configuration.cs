using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyChanged.SourceGenerator
{
    public enum Capitalisation
    {
        None,
        Uppercase,
        Lowercase,
    }

    public class Configuration
    {
        public string OnPropertyChangedMethodName { get; set; } = "OnPropertyChanged";

        public string[] RemovePrefixes { get; set; } = new[] { "_" };
        public string[] RemoveSuffixes { get; set; } = Array.Empty<string>();
        public string? AddPrefix { get; set; } = null;
        public string? AddSuffix { get; set; } = null;
        public Capitalisation FirstLetterCapitalisation { get; set; } = Capitalisation.Uppercase; 
    }
}
