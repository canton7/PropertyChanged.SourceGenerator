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
                this.diagnostics.ReportUnknownFirstLetterCapitalisation(firstLetterCapitalisation);
            }
        }
        config.EnableAutoNotify = this.ParseBool(options, "propertychanged.auto_notify", true);

        return config;
    }

    private bool ParseBool(AnalyzerConfigOptions options, string key, bool defaultValue)
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

            this.diagnostics.ReportCannotParseConfigBool(key, value);
            return defaultValue;
        }
        else
        {
            return defaultValue;
        }
    }
}
