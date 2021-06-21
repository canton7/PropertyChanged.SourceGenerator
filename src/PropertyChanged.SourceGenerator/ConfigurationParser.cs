using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PropertyChanged.SourceGenerator
{
    public class ConfigurationParser
    {
        private readonly AnalyzerConfigOptionsProvider optionsProvider;
        private readonly DiagnosticReporter diagnostics;

        public ConfigurationParser(AnalyzerConfigOptionsProvider optionsProvider, DiagnosticReporter diagnostics)
        {
            this.optionsProvider = optionsProvider;
            this.diagnostics = diagnostics;
        }

        public Configuration Parse(SyntaxTree? syntaxTree)
        {
            var options = syntaxTree == null
                ? this.optionsProvider.GlobalOptions
                : this.optionsProvider.GetOptions(syntaxTree);
            var config = new Configuration();

            if (options.TryGetValue("propertychanged_onpropertychanged_method_name", out string? methodName))
            {
                config.RaisePropertyChangedMethodNames = methodName.Split(';');
            }
            if (options.TryGetValue("propertychanged_remove_prefixes", out string? removePrefixes))
            {
                config.RemovePrefixes = removePrefixes.Split(';');
            }
            if (options.TryGetValue("propertychanged_remove_suffixes", out string? removeSuffixes))
            {
                config.RemoveSuffixes = removeSuffixes.Split(';');
            }
            if (options.TryGetValue("propertychanged_add_prefix", out string? addPrefix))
            {
                config.AddPrefix = addPrefix;
            }
            if (options.TryGetValue("propertychanged_add_suffix", out string? addSuffix))
            {
                config.AddSuffix = addSuffix;
            }
            if (options.TryGetValue("propertychanged_first_letter_capitalization", out string? firstLetterCapitalisation))
            {
                if (string.Equals(firstLetterCapitalisation, "none", StringComparison.OrdinalIgnoreCase))
                {
                    config.FirstLetterCapitalisation = Capitalisation.None;
                }
                else if (string.Equals(firstLetterCapitalisation, "upper_case", StringComparison.OrdinalIgnoreCase))
                {
                    config.FirstLetterCapitalisation = Capitalisation.Uppercase;
                }
                else if (string.Equals(firstLetterCapitalisation, "lower_case", StringComparison.OrdinalIgnoreCase))
                {
                    config.FirstLetterCapitalisation = Capitalisation.Lowercase;
                }
                else
                {
                    this.diagnostics.ReportUnknownFirstLetterCapitalisation(firstLetterCapitalisation);
                }
            }

            return config;
        }
    }
}
