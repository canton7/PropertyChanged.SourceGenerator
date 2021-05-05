using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropertyChanged.SourceGenerator.UnitTests.Framework
{
    public class DiagnosticResult
    {
        public string Code { get; }
        public string SquiggledText { get; }
        public bool IsError { get; set; } = true;
        public List<DiagnosticResultLocation> Locations { get; } = new List<DiagnosticResultLocation>();

        public DiagnosticResult(string code, string squiggledText)
        {
            this.Code = code;
            this.SquiggledText = squiggledText;
        }

        public DiagnosticResult WithLocation(int line, int column)
        {
            this.Locations.Add(new DiagnosticResultLocation(line, column));
            return this;
        }

    }

    public struct DiagnosticResultLocation
    {
        public int Line { get; }
        public int Column { get; }

        public DiagnosticResultLocation(int line, int column)
        {
            if (column < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(column), "column must be >= -1");
            }

            this.Line = line;
            this.Column = column;
        }
    }
}
