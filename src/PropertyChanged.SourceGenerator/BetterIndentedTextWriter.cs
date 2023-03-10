using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace PropertyChanged.SourceGenerator;

public class BetterIndentedTextWriter : IndentedTextWriter
{
    public BetterIndentedTextWriter() : base(new StringWriter())
    {
    }

    public void Write([InterpolatedStringHandlerArgument("")] ref AppendInterpolatedStringHandler handler) { }
    public void WriteLine([InterpolatedStringHandlerArgument("")] ref AppendInterpolatedStringHandler handler) => this.WriteLine();
}

[InterpolatedStringHandler]
public struct AppendInterpolatedStringHandler
{
    private readonly BetterIndentedTextWriter writer;

    public AppendInterpolatedStringHandler(int literalLength, int formattedCount, BetterIndentedTextWriter writer)
    {
        this.writer = writer;
    }

    public void AppendLiteral(string value) => this.writer.Write(value);
    public void AppendFormatted(string? value) => this.writer.Write(value);

    public void AppendFormatted<T>(T value)
    {
        if (value is not null)
        {
            this.writer.Write(value.ToString());
        }
    }
}
