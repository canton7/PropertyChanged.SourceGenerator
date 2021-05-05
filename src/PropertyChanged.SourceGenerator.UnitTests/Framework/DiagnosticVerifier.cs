using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace PropertyChanged.SourceGenerator.UnitTests.Framework
{
    public static class DiagnosticVerifier
    {
        public static void VerifyDiagnostics(
            IEnumerable<Diagnostic> diagnostics,
            DiagnosticResult[] expected)
        {
            var sortedDiagnostics = diagnostics.OrderBy(d => d.Location.SourceSpan.Start).ToList();
            VerifyDiagnosticResults(sortedDiagnostics, expected);
        }

        private static void VerifyDiagnosticResults(
            List<Diagnostic> actualResults,
            DiagnosticResult[] expectedResults)
        {
            int expectedCount = expectedResults.Length;
            int actualCount = actualResults.Count;

            if (expectedCount != actualCount)
            {
                string diagnosticsOutput = actualResults.Any() ? FormatDiagnostics(actualResults.ToArray()) : "    NONE.";

                Assert.Fail(string.Format("Mismatch between number of diagnostics returned, expected \"{0}\" actual \"{1}\"\r\n\r\nDiagnostics:\r\n{2}\r\n", expectedCount, actualCount, diagnosticsOutput));
            }

            for (int i = 0; i < expectedResults.Length; i++)
            {
                var actual = actualResults[i];
                var expected = expectedResults[i];

                if (expected.Locations.Count == 0)
                {
                    if (actual.Location != Location.None)
                    {
                        Assert.Fail(string.Format("Expected:\nA project diagnostic with No location\nActual:\n{0}",
                            FormatDiagnostics(actual)));
                    }
                }
                else
                {
                    VerifyDiagnosticLocation(actual, actual.Location, expected.Locations.First());
                    var additionalLocations = actual.AdditionalLocations.ToArray();

                    if (additionalLocations.Length != expected.Locations.Count - 1)
                    {
                        Assert.True(false,
                            string.Format("Expected {0} additional locations but got {1} ({2}) for Diagnostic:\r\n{3}\r\n",
                                expected.Locations.Count - 1, additionalLocations.Length,
                                string.Join(", ", additionalLocations.Select(x =>
                                {
                                    var start = x.GetLineSpan().StartLinePosition;
                                    return $"({start.Line + 1},{start.Character + 1})";
                                })),
                                FormatDiagnostics(actual)));
                    }

                    for (int j = 0; j < additionalLocations.Length; ++j)
                    {
                        VerifyDiagnosticLocation(actual, additionalLocations[j], expected.Locations[j + 1]);
                    }
                }

                if (actual.Id != expected.Code)
                {
                    Assert.Fail(string.Format("Expected diagnostic id to be \"{0}\" ({1}) was \"{2}\"\r\n\r\nDiagnostic:\r\n{3}\r\n", expected.Code, expected.Code, actual.Id, FormatDiagnostics(actual)));
                }

                var expectedSeverity = expected.IsError ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning;
                if (actual.Severity != expectedSeverity)
                {
                    Assert.Fail(string.Format("Expected diagnostic severity to be \"{0}\" was \"{1}\"\r\n\r\nDiagnostic:\r\n{2}\r\n", expectedSeverity, actual.Severity, FormatDiagnostics(actual)));
                }

                string squiggledText = GetSquiggledText(actual);
                if (squiggledText != expected.SquiggledText)
                {
                    Assert.Fail(string.Format("Expected squiggled text to be \"{0}\", was \"{1}\"\r\n\r\nDiagnostic:\r\n{2}\r\n", expected.SquiggledText, squiggledText, FormatDiagnostics(actual)));
                }

                //if (actual.GetMessage() != expected.Message)
                //{
                //    Assert.True(false,
                //        string.Format("Expected diagnostic message to be \"{0}\" was \"{1}\"\r\n\r\nDiagnostic:\r\n    {2}\r\n",
                //            expected.Message, actual.GetMessage(), FormatDiagnostics(actual)));
                //}
            }
        }

        /// <summary>
        /// Helper method to VerifyDiagnosticResult that checks the location of a diagnostic and compares it with the location in the expected DiagnosticResult.
        /// </summary>
        private static void VerifyDiagnosticLocation(
            Diagnostic diagnostic,
            Location actual,
            DiagnosticResultLocation expected)
        {
            var actualSpan = actual.GetLineSpan();

            //Assert.True(actualSpan.Path == expected.Path || (actualSpan.Path != null && actualSpan.Path.Contains("Test0.") && expected.Path.Contains("Test.")),
            //    string.Format("Expected diagnostic to be in file \"{0}\" was actually in file \"{1}\"\r\n\r\nDiagnostic:\r\n    {2}\r\n",
            //        expected.Path, actualSpan.Path, FormatDiagnostics(diagnostic)));

            var actualLinePosition = actualSpan.StartLinePosition;

            // Only check line position if there is an actual line in the real diagnostic
            if (actualLinePosition.Line > 0)
            {
                if (actualLinePosition.Line + 1 != expected.Line)
                {
                    Assert.Fail(string.Format("Expected diagnostic to be on line \"{0}\" was actually on line \"{1}\"\r\n\r\nDiagnostic:\r\n{2}\r\n", expected.Line, actualLinePosition.Line + 1, FormatDiagnostics(diagnostic)));
                }
            }

            // Only check column position if there is an actual column position in the real diagnostic
            if (actualLinePosition.Character > 0)
            {
                if (actualLinePosition.Character + 1 != expected.Column)
                {
                    Assert.Fail(string.Format("Expected diagnostic to start at column \"{0}\" was actually at column \"{1}\"\r\n\r\nDiagnostic:\r\n{2}\r\n", expected.Column, actualLinePosition.Character + 1, FormatDiagnostics(diagnostic)));
                }
            }
        }

        private static string FormatDiagnostics(params Diagnostic[] diagnostics)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < diagnostics.Length; ++i)
            {
                var location = diagnostics[i].Location;

                int line = 0;
                int col = 0;
                if (location != Location.None)
                {
                    var mappedSpan = location.GetMappedLineSpan().Span;
                    line = mappedSpan.Start.Line + 1;
                    col = mappedSpan.Start.Character + 1;
                }

                builder.AppendFormat("// ({0},{1}): {2} {3}: {4}",
                    line,
                    col,
                    diagnostics[i].Severity,
                    diagnostics[i].Id,
                    diagnostics[i].GetMessage(null)).AppendLine();

                string squiggledText = "";
                if (location == Location.None)
                {
                    builder.AppendFormat("// {0}.{1}", diagnostics[i].Descriptor.Title, diagnostics[i].Id).AppendLine();
                }
                else
                {
                    Assert.True(location.IsInSource,
                        $"Test base does not currently handle diagnostics in metadata locations. Diagnostic in metadata: {diagnostics[i]}\r\n");

                    squiggledText = GetSquiggledText(diagnostics[i]);
                    builder.AppendFormat("// {0}", squiggledText).AppendLine();
                }

                builder.AppendFormat("Diagnostic(\"{0}\", @\"{1}\")",
                    diagnostics[i].Id,
                    squiggledText.Replace("\"", "\"\""));
                if (location != Location.None)
                {
                    builder.AppendFormat(".WithLocation({0}, {1})",
                        line,
                        col);
                }

                builder.AppendLine().AppendLine();
            }
            return builder.ToString();
        }

        private static string GetSquiggledText(Diagnostic diagnostic)
        {
            return diagnostic.Location == Location.None
                ? ""
                : diagnostic.Location.SourceTree?.GetText().ToString(diagnostic.Location.SourceSpan) ?? "";
        }
    }

}
