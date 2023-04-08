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
    internal class AtomAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor UnhandledErrorRule = new(
            id: "UniMob_000",
            title: "Internal error",
            messageFormat: "Internal error occurred. Please open a bug report. Message: '{0}'",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        private static readonly DiagnosticDescriptor AtomAttributeCanBeUsedOnlyOnClassMembers = new(
            id: "UniMob_011",
            title: "Atom attribute can be used only on class members",
            messageFormat: "Atom attribute can be used only on class members",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        private static readonly DiagnosticDescriptor AtomAttributeCanBeUsedOnlyOnLifetimeScope = new(
            id: "UniMob_012",
            title: "Atom attribute can be used only on class that implements ILifetimeScope interface",
            messageFormat: "Atom attribute can be used only on class that implements ILifetimeScope interface",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        private static readonly DiagnosticDescriptor AtomAttributeCannotBeUsedOnGenericClasses = new(
            id: "UniMob_013",
            title: "Atom attribute cannot be used on generic classes",
            messageFormat: "Atom attribute cannot be used on generic classes",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        private static readonly DiagnosticDescriptor CannotUseAtomAttributeOnSetOnlyProperty = new(
            id: "UniMob_021",
            title: "Atom attribute cannot be used on set-only property",
            messageFormat: "Atom attribute cannot be used on set-only property",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        private static readonly DiagnosticDescriptor CannotUseAtomAttributeOnStaticProperty = new(
            id: "UniMob_022",
            title: "Atom attribute cannot be used on static property",
            messageFormat: "Atom attribute cannot be used on static property",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        private static readonly DiagnosticDescriptor CannotUseAtomAttributeOnAbstractProperty = new(
            id: "UniMob_023",
            title: "Atom attribute cannot be used on abstract property",
            messageFormat: "Atom attribute cannot be used on abstract property",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            AtomAttributeCanBeUsedOnlyOnClassMembers,
            AtomAttributeCannotBeUsedOnGenericClasses,
            AtomAttributeCanBeUsedOnlyOnLifetimeScope,
            CannotUseAtomAttributeOnSetOnlyProperty,
            CannotUseAtomAttributeOnStaticProperty,
            CannotUseAtomAttributeOnAbstractProperty,
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
            if (context.Compilation.GetTypeByMetadataName("UniMob.AtomAttribute") is not { } atomAttributeSymbol)
            {
                return;
            }

            if (context.Compilation.GetTypeByMetadataName("UniMob.ILifetimeScope") is not { } lifetimeScopeSymbol)
            {
                return;
            }

            var cache = new Cache
            {
                AtomAttributeTypeSymbol = atomAttributeSymbol,
                LifetimeScopeTypeSymbol = lifetimeScopeSymbol,
            };

            context.RegisterSyntaxNodeAction(ctx =>
            {
                try
                {
                    CheckAttributeDeclaration(ctx, cache);
                }
                catch (Exception ex)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(UnhandledErrorRule, ctx.Node.GetLocation(), ex.Message));
                }
            }, SyntaxKind.Attribute);
        }

        private void CheckAttributeDeclaration(SyntaxNodeAnalysisContext context, Cache cache)
        {
            if (context.Node.Ancestors().OfType<PropertyDeclarationSyntax>().FirstOrDefault()
                is not { } propertySyntax)
            {
                return;
            }

            if (context.SemanticModel.GetDeclaredSymbol(propertySyntax) is not { } propertySymbol)
            {
                return;
            }

            var atomAttributeData = propertySymbol.GetAttributes().FirstOrDefault(it =>
                SymbolEqualityComparer.Default.Equals(cache.AtomAttributeTypeSymbol, it.AttributeClass));

            if (atomAttributeData is null)
            {
                return;
            }

            AnalyzeAtomProperty(context, cache, propertySyntax, propertySymbol);
        }

        private void AnalyzeAtomProperty(SyntaxNodeAnalysisContext context, Cache cache,
            PropertyDeclarationSyntax propertySyntax, IPropertySymbol propertySymbol)
        {
            if (propertySymbol.IsStatic)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(CannotUseAtomAttributeOnStaticProperty, context.Node.GetLocation()));
            }

            if (propertySymbol.IsAbstract)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(CannotUseAtomAttributeOnAbstractProperty, context.Node.GetLocation()));
            }

            if (propertySymbol.GetMethod is null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(CannotUseAtomAttributeOnSetOnlyProperty, context.Node.GetLocation()));
            }

            if (propertySyntax.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()
                is not { } classSyntax)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(AtomAttributeCanBeUsedOnlyOnClassMembers, context.Node.GetLocation()));
                return;
            }

            if (classSyntax.TypeParameterList != null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(AtomAttributeCannotBeUsedOnGenericClasses, context.Node.GetLocation()));
            }

            if (context.SemanticModel.GetDeclaredSymbol(classSyntax) is not { } classSymbol)
            {
                return;
            }

            var isLifetimeScope = classSymbol.AllInterfaces
                .Any(it => SymbolEqualityComparer.Default.Equals(cache.LifetimeScopeTypeSymbol, it));

            if (!isLifetimeScope)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(AtomAttributeCanBeUsedOnlyOnLifetimeScope, context.Node.GetLocation()));
            }
        }

        private class Cache
        {
            public INamedTypeSymbol AtomAttributeTypeSymbol;
            public INamedTypeSymbol LifetimeScopeTypeSymbol;
        }
    }
}