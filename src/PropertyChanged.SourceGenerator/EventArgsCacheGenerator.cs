using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using PropertyChanged.SourceGenerator;
using PropertyChanged.SourceGenerator.EventArgs;

public class EventArgsCacheGenerator
{
    public const string EventArgsCacheName = "EventArgsCache";

    private readonly BetterIndentedTextWriter writer = new();
    private readonly EventArgsCache eventArgsCache;

    public EventArgsCacheGenerator(EventArgsCache eventArgsCache)
    {
        this.eventArgsCache = eventArgsCache;
    }

    public void GenerateNameCache()
    {
        this.writer.WriteLine("namespace PropertyChanged.SourceGenerator.Internal");
        this.writer.WriteLine("{");
        this.writer.Indent++;

        this.writer.WriteLine($"internal static class {EventArgsCacheName}");
        this.writer.WriteLine("{");
        this.writer.Indent++;

        var backingFieldNames = new HashSet<string>();
        foreach (var (cacheName, eventArgsTypeName, propertyName) in this.eventArgsCache.GetEntries())
        {
            string backingFieldName = "_" + cacheName;
            for (int i = 0; !backingFieldNames.Add(backingFieldName); i++)
            {
                backingFieldName = $"_{cacheName}{i}";
            }
            this.writer.WriteLine($"private static {eventArgsTypeName} {backingFieldName};");
            this.writer.WriteLine($"public static {eventArgsTypeName} {cacheName} => " +
                $"{backingFieldName} ??= new {eventArgsTypeName}({EscapeString(propertyName)});");
        }

        this.writer.Indent--;
        this.writer.WriteLine("}");

        this.writer.Indent--;
        this.writer.WriteLine("}");
    }

    private static string EscapeString(string? str)
    {
        return str == null
            ? "null"
            : "@\"" + str.Replace("\"", "\"\"") + "\"";
    }

    public SourceText ToSourceText() => this.writer.ToSourceText();
}
