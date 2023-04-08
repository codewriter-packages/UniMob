using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace UniMob.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AtomContainerAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor UnhandledErrorRule = new(
            id: "UniMob_100",
            title: "Internal error",
            messageFormat: "Internal error occurred. Please open a bug report. Message: '{0}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        private static readonly DiagnosticDescriptor AtomContainerAttributeCanBeUsedOnlyOnLifetimeScope = new(
            id: "UniMob_101",
            title: "AtomContainer attribute can be used only on class with implemented ILifetimeScope interface",
            messageFormat:
            "AtomContainer attribute can be used only on class with implemented ILifetimeScope interface",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        private static readonly DiagnosticDescriptor AtomContainerAttributeCannotBeUsedOnGenericClasses = new(
            id: "UniMob_102",
            title: "AtomContainer attribute cannot be used on generic classes",
            messageFormat: "AtomContainer attribute cannot be used on generic classes",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            AtomContainerAttributeCanBeUsedOnlyOnLifetimeScope,
            AtomContainerAttributeCannotBeUsedOnGenericClasses,
            UnhandledErrorRule
        );

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze |
                                                   GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            if (context.Compilation.GetTypeByMetadataName("UniMob.AtomContainerAttribute")
                is not { } atomContainerAttributeSymbol)
            {
                return;
            }

            if (context.Compilation.GetTypeByMetadataName("UniMob.ILifetimeScope")
                is not { } lifetimeScopeSymbol)
            {
                return;
            }

            var cache = new Cache
            {
                AtomContainerAttributeTypeSymbol = atomContainerAttributeSymbol,
                LifetimeScopeTypeSymbol = lifetimeScopeSymbol,
            };

            context.RegisterSyntaxNodeAction(ctx =>
            {
                try
                {
                    CheckClassDeclaration(ctx, cache);
                }
                catch (Exception ex)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(UnhandledErrorRule, ctx.Node.GetLocation(), ex.Message));
                }
            }, SyntaxKind.ClassDeclaration);
        }

        private void CheckClassDeclaration(SyntaxNodeAnalysisContext context, Cache cache)
        {
            if (context.Node is not ClassDeclarationSyntax classSyntax)
            {
                return;
            }

            if (classSyntax.AttributeLists.Count == 0)
            {
                return;
            }

            if (context.SemanticModel.GetDeclaredSymbol(classSyntax) is not { } classSymbol)
            {
                return;
            }

            var atomContainerAttributeData = classSymbol.GetAttributes().FirstOrDefault(it =>
                SymbolEqualityComparer.Default.Equals(cache.AtomContainerAttributeTypeSymbol, it.AttributeClass));

            if (atomContainerAttributeData is null)
            {
                return;
            }

            AnalyzeAtomContainer(context, cache, classSyntax, classSymbol);
        }

        private void AnalyzeAtomContainer(SyntaxNodeAnalysisContext context, Cache cache,
            ClassDeclarationSyntax classSyntax, INamedTypeSymbol classSymbol)
        {
            if (classSyntax.TypeParameterList != null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(AtomContainerAttributeCannotBeUsedOnGenericClasses, classSyntax.GetLocation()));
            }

            var isLifetimeScope = classSymbol.AllInterfaces
                .Any(it => SymbolEqualityComparer.Default.Equals(cache.LifetimeScopeTypeSymbol, it));

            if (!isLifetimeScope)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(AtomContainerAttributeCanBeUsedOnlyOnLifetimeScope,
                        classSyntax.Identifier.GetLocation()));
            }
        }

        private class Cache
        {
            public INamedTypeSymbol AtomContainerAttributeTypeSymbol;
            public INamedTypeSymbol LifetimeScopeTypeSymbol;
        }
    }
}