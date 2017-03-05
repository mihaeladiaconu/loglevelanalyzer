using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace LogLevelAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class LogLevelAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "LogLevelAnalyzer";

        private static readonly LocalizableString Title = 
            new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat =
            new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description =
            new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Performance";

        private static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(Rule); }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var invocationExpr = (InvocationExpressionSyntax) context.Node;
            var methodName = invocationExpr.Expression as MemberAccessExpressionSyntax;

            if (methodName == null) return;

            if (!methodName.Name.ToString().StartsWith("Debug"))
            {
                return;
            }

            var memberSymbol = context.SemanticModel.GetSymbolInfo(methodName).Symbol as IMethodSymbol;
            if (!memberSymbol?.ToString().StartsWith("log4net.ILog.") ?? true) return;

            var ancestors =
                methodName.Ancestors()
                    .Where(node => node.Kind() == SyntaxKind.IfStatement)
                    .Cast<IfStatementSyntax>()
                    .ToList();

            var methodLevel = $"Is{methodName.Name.ToString().Replace("Format", "")}Enabled";
            ExpressionSyntax condition =
                ancestors.Where(node => node.Condition.ToString().Contains(methodLevel))
                    .Select(node => node.Condition).FirstOrDefault();
            if (condition == null)
            {
                AddLogLevelDiagnostic(context, methodName, methodLevel);
                return;
            }

            var identifier = methodName.GetFirstToken().ValueText;
            var accessExpr = condition as MemberAccessExpressionSyntax;
            if (accessExpr != null)
            {
                AnalyzeIfCondition(context, accessExpr, identifier, methodName, methodLevel);
                return;
            }

            foreach (var childNode in condition.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                AnalyzeIfCondition(context, childNode, identifier, methodName, methodLevel);
            }
        }

        private static void AnalyzeIfCondition(SyntaxNodeAnalysisContext context, MemberAccessExpressionSyntax ifNode,
            string identifier, MemberAccessExpressionSyntax memberAccessExpr, string methodLevel)
        {
            if (ifNode.Parent.IsKind(SyntaxKind.LogicalNotExpression) || ifNode.GetFirstToken().ValueText != identifier)
            {
                AddLogLevelDiagnostic(context, memberAccessExpr, methodLevel);
            }
        }

        private static void AddLogLevelDiagnostic(SyntaxNodeAnalysisContext context,
            CSharpSyntaxNode memberAccessExpr, string methodLevel)
        {
            var diagnostic = Diagnostic.Create(Rule, memberAccessExpr.GetLocation(), methodLevel);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
