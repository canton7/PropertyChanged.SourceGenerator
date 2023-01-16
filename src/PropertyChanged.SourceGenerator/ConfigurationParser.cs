using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PropertyChanged.SourceGenerator;

public class ConfigurationParser
{
    private readonly AnalyzerConfigOptionsProvider optionsProvider;

    public ConfigurationParser(AnalyzerConfigOptionsProvider optionsProvider)
    {
        this.optionsProvider = optionsProvider;
    }

    public Configuration Parse(SyntaxTree? syntaxTree, DiagnosticReporter diagnostics)
    {
        var options = syntaxTree == null
            ? this.optionsProvider.GlobalOptions
            : this.optionsProvider.GetOptions(syntaxTree);
        var config = new Configuration();

        if (options.TryGetValue("propertychanged.onpropertychanged_method_name", out string? changedMethodName))
        {
            config.RaisePropertyChangedMethodNames = changedMethodName.Split(';');
        }
        if (options.TryGetValue("propertychanged.onpropertychanging_method_name", out string? changingMethodName))
        {
            config.RaisePropertyChangingMethodNames = changingMethodName.Split(';');
        }
        if (options.TryGetValue("propertychanged.remove_prefixes", out string? removePrefixes))
        {
            config.RemovePrefixes = removePrefixes.Split(';');
        }
        if (options.TryGetValue("propertychanged.remove_suffixes", out string? removeSuffixes))
        {
            config.RemoveSuffixes = removeSuffixes.Split(';');
        }
        if (options.TryGetValue("propertychanged.add_prefix", out string? addPrefix))
        {
            config.AddPrefix = addPrefix;
        }
        if (options.TryGetValue("propertychanged.add_suffix", out string? addSuffix))
        {
            config.AddSuffix = addSuffix;
        }
        if (options.TryGetValue("propertychanged.first_letter_capitalization", out string? firstLetterCapitalisation))
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
                diagnostics.ReportUnknownFirstLetterCapitalisation(firstLetterCapitalisation);
            }
        }
        config.EnableAutoNotify = this.ParseBool(options, diagnostics, "propertychanged.auto_notify", true);

        return config;
    }

    private bool ParseBool(AnalyzerConfigOptions options, DiagnosticReporter diagnostics, string key, bool defaultValue)
    {
        if (options.TryGetValue(key, out string? value))
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            diagnostics.ReportCannotParseConfigBool(key, value);
            return defaultValue;
        }
        else
        {
            return defaultValue;
        }
    }
}
