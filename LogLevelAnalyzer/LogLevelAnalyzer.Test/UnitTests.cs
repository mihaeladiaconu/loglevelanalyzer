using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace LogLevelAnalyzer.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {
        [TestMethod]
        public void NoErrorByDefault()
        {
            VerifyCSharpDiagnostic(@"");
        }

        [TestMethod]
        public void ErrorsOnDebugWithoutIf()
        {
            DiagnosticResult expected = GetExpectedDiagnostic("Check 'IsDebugEnabled' before logging", 13, 26);
            Verify(@"Log.DebugFormat(""Hello world"");", expected);
        }

        [TestMethod]
        public void ErrorsOnDebugWithIfWithoutLevelCheck()
        {
            DiagnosticResult expected = GetExpectedDiagnostic("Check 'IsDebugEnabled' before logging", 13, 38);
            Verify(@"if (true) { Log.Debug(""Hello world"") }", expected);
        }

        [TestMethod]
        public void ErrorsOnDebugWithNegatedLevelCheck()
        {
            DiagnosticResult expected = GetExpectedDiagnostic("Check 'IsDebugEnabled' before logging", 13, 53);
            Verify(@"if (!Log.IsDebugEnabled) { Log.Debug(""Hello world"") }", expected);
        }

        [TestMethod]
        public void ErrorsOnDebugWithWrongLoggerCheck()
        {
            DiagnosticResult expected = GetExpectedDiagnostic("Check 'IsDebugEnabled' before logging", 13, 143);
            Verify(@"var anotherLog = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);" +
                   @"if (anotherLog.IsDebugEnabled) {{ Log.Debug(""Hello world"") }}",
                expected);
        }

        [TestMethod]
        public void IgnoresDebugWithLevelCheck()
        {
            Verify(@"if (Log.IsDebugEnabled)
                    {
                        Log.Debug(""Hello world"");
                    }");
        }

        [TestMethod]
        public void IgnoresDebugWithLevelCheckMultipleBlocksAway()
        {
            Verify(@"if (Log.IsDebugEnabled)
                    {
                        if(true)
                        {
                            Log.Debug(""Hello world"");
                        }
                    }");
        }

        [TestMethod]
        public void IgnoresDebugWithLevelMultipleChecksMultipleBlocksAway()
        {
            Verify(@"if (Log.IsDebugEnabled)
                    {
                        if(Log.IsWarnEnabled)
                        {
                            Log.Debug(""Hello world"");
                        }
                    }");
        }

        [TestMethod]
        public void IgnoresDebugFormatWithLevelCheck()
        {
            Verify(@"if (Log.IsDebugEnabled)
                    {
                        Log.DebugFormat(""Hello {0}"", ""world"");
                    }");
        }

        private static DiagnosticResult GetExpectedDiagnostic(string message, int line, int column)
        {
            return new DiagnosticResult
            {
                Id = "LogLevelAnalyzer",
                Message = message,
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[]
                    {
                        new DiagnosticResultLocation("Test0.cs", line, column)
                    }
            };
        }

        private void Verify(string sut, params DiagnosticResult[] expected)
        {
            VerifyCSharpDiagnostic(@"
                using log4net;
                using System.Reflection;

                namespace ConsoleApplication5
                {
                    public class Program
                    {
                        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

                        public static void Main()
                        {
                         " + sut + @"
                        }
                    } 
                }", expected);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new LogLevelAnalyzerCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new LogLevelAnalyzer();
        }
    }
}