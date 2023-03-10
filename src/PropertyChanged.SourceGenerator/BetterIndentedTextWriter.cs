using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace PropertyChanged.SourceGenerator;

public class BetterIndentedTextWriter : IndentedTextWriter
{
    public BetterIndentedTextWriter() : base(new StringWriter())
    {
    }

    public void Write([InterpolatedStringHandlerArgument("")] ref AppendInterpolatedStringHandler handler) { }
    public void WriteLine([InterpolatedStringHandlerArgument("")] ref AppendInterpolatedStringHandler handler) => this.WriteLine();

    public SourceText ToSourceText()
    {
        var stringWriter = (StringWriter)this.InnerWriter;
        var stringBuilder = stringWriter.GetStringBuilder();
        stringWriter.Close(); // Attempts to interact with it after this point will fail
        return new StringBuilderText(stringBuilder);
    }

    private class StringBuilderText : SourceText
    {
        private readonly StringBuilder stringBuilder;

        public StringBuilderText(StringBuilder stringBuilder)
        {
            this.stringBuilder = stringBuilder;
        }
        public override Encoding? Encoding => Encoding.UTF8;

        public override char this[int position] => this.stringBuilder[position];

        public override int Length => this.stringBuilder.Length;

        public override void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) =>
            this.stringBuilder.CopyTo(sourceIndex, destination, destinationIndex, count);

        public override string ToString(TextSpan span)
        {
            if (span.End > this.stringBuilder.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(span));
            }

            return this.stringBuilder.ToString(span.Start, span.Length);
        }
    }
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
