using AmazingMCP.Models;
using AmazingMCP.Models.UsageQuery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace AmazingMCP.Services.UsageQuery;

/// <summary>
/// Creates <see cref="QueryEntry"/> instances from syntax nodes using semantic model information.
/// Returns null when the node does not represent a trackable usage.
/// </summary>
public static class QueryEntryFactory
{
    public static IEnumerable<QueryEntry> TryCreate(SyntaxNode node, SemanticModel model)
    {
        var entry = node switch
        {
            InvocationExpressionSyntax invocation           => FromInvocation(invocation, model),
            ObjectCreationExpressionSyntax ctor             => FromObjectCreation(ctor, model),
            ImplicitObjectCreationExpressionSyntax implCtor => FromImplicitObjectCreation(implCtor, model),
            MemberAccessExpressionSyntax memberAccess       => FromMemberAccess(memberAccess, model),
            IdentifierNameSyntax identifier                 => FromIdentifier(identifier, model),
            GenericNameSyntax generic                       => FromGenericName(generic, model),
            TypeConstraintSyntax constraint                 => FromTypeConstraint(constraint, model),
            ParameterSyntax parameter                       => FromParameter(parameter, model),
            TypeOfExpressionSyntax typeOf                   => FromTypeOf(typeOf, model),
            BinaryExpressionSyntax binary                   => FromBinaryExpression(binary, model),
            AssignmentExpressionSyntax assignment           => FromEventAssignment(assignment, model),
            _ => null,
        };

        if (entry is null) yield break;
        yield return entry;

        var extensionEntry = TryCreateExtensionMethodEntry(node, entry, model);
        if (extensionEntry is not null)
            yield return extensionEntry;
    }

    static QueryEntry? TryCreateExtensionMethodEntry(SyntaxNode node, QueryEntry receiverEntry, SemanticModel model)
    {
        if (node is not InvocationExpressionSyntax inv) return null;
        if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol { IsExtensionMethod: true } sym)
            return null;

        // For null-conditional calls (obj?.ExtMethod()), report the call on the receiver type, not the extension class
        if (inv.Expression is MemberBindingExpressionSyntax)
        {
            var conditionalAccess = inv.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>();
            if (conditionalAccess is not null)
            {
                var t = model.GetTypeInfo(conditionalAccess.Expression).Type;
                // Strip nullable annotation: IOptionalTracer? → IOptionalTracer
                var receiverType = t?.WithNullableAnnotation(NullableAnnotation.None);
                return new QueryEntry
                {
                    Kind = UsageKind.MethodCall,
                    TypeName = receiverType?.ToDisplayString() ?? sym.ContainingType.ToDisplayString(),
                    MethodName = sym.Name,
                    ArgumentTypes = receiverEntry.ArgumentTypes,
                };
            }
        }

        return new QueryEntry
        {
            Kind = UsageKind.MethodCall,
            TypeName = sym.ContainingType.ToDisplayString(),
            MethodName = sym.Name,
            ArgumentTypes = receiverEntry.ArgumentTypes,
        };
    }

    // ── Invocation ────────────────────────────────────────────────────────────

    static QueryEntry? FromInvocation(InvocationExpressionSyntax node, SemanticModel model)
    {
        if (model.GetOperation(node) is INameOfOperation)
            return FromNameOf(node, model);

        var symbol = model.GetSymbolInfo(node).Symbol as IMethodSymbol;
        if (symbol is null) return null;

        return TryEventCallFromConditionalInvoke(node, model)
            ?? TryEventCallFromDirectInvocation(node, model)
            ?? MethodCallEntry(node, symbol, model);
    }

    static QueryEntry? TryEventCallFromConditionalInvoke(InvocationExpressionSyntax node, SemanticModel model)
    {
        if (node.Expression is not MemberBindingExpressionSyntax { Name.Identifier.Text: "Invoke" })
            return null;

        var conditionalAccess = node.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>();
        if (conditionalAccess is null) return null;

        var eventSymbol = model.GetSymbolInfo(conditionalAccess.Expression).Symbol as IEventSymbol;
        if (eventSymbol is null) return null;

        return new QueryEntry
        {
            Kind = UsageKind.EventCall,
            TypeName = eventSymbol.ContainingType.ToDisplayString(),
            EventName = eventSymbol.Name,
        };
    }

    static QueryEntry? TryEventCallFromDirectInvocation(InvocationExpressionSyntax node, SemanticModel model)
    {
        if (node.Expression is not IdentifierNameSyntax id) return null;
        if (model.GetSymbolInfo(id).Symbol is not IEventSymbol eventSymbol) return null;

        return new QueryEntry
        {
            Kind = UsageKind.EventCall,
            TypeName = eventSymbol.ContainingType.ToDisplayString(),
            EventName = eventSymbol.Name,
        };
    }

    static QueryEntry MethodCallEntry(InvocationExpressionSyntax node, IMethodSymbol symbol, SemanticModel model)
    {
        var typeName = node.Expression is MemberAccessExpressionSyntax memberAccess
            ? model.GetTypeInfo(memberAccess.Expression).Type?.ToDisplayString() ?? symbol.ContainingType.ToDisplayString()
            : symbol.ContainingType.ToDisplayString();

        return new QueryEntry
        {
            Kind = UsageKind.MethodCall,
            TypeName = typeName,
            MethodName = symbol.Name,
            ArgumentTypes = GetArgumentTypes(node.ArgumentList, model),
        };
    }

    // ── Object creation ───────────────────────────────────────────────────────

    static QueryEntry? FromObjectCreation(ObjectCreationExpressionSyntax node, SemanticModel model)
    {
        var symbol = model.GetSymbolInfo(node).Symbol as IMethodSymbol;
        if (symbol is null) return null;

        return new QueryEntry
        {
            Kind = UsageKind.ConstructorCall,
            TypeName = symbol.ContainingType.ToDisplayString(),
            MethodName = symbol.ContainingType.Name,
            ArgumentTypes = GetArgumentTypes(node.ArgumentList, model),
        };
    }

    static QueryEntry? FromImplicitObjectCreation(ImplicitObjectCreationExpressionSyntax node, SemanticModel model)
    {
        if (model.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol) return null;

        return new QueryEntry
        {
            Kind = UsageKind.ConstructorCall,
            TypeName = symbol.ContainingType.ToDisplayString(),
            MethodName = symbol.ContainingType.Name,
            ArgumentTypes = GetArgumentTypes(node.ArgumentList, model),
        };
    }

    // ── Member access (property / field read) ─────────────────────────────────

    static QueryEntry? FromMemberAccess(MemberAccessExpressionSyntax node, SemanticModel model)
    {
        // Skip if this is the expression part of an invocation — handled by FromInvocation
        if (node.Parent is InvocationExpressionSyntax inv && inv.Expression == node)
            return null;

        var symbol = model.GetSymbolInfo(node).Symbol;
        return symbol switch
        {
            IPropertySymbol prop => BuildPropertyEntry(node, prop, model),
            IFieldSymbol field   => BuildFieldEntry(node, field, model),
            _                    => null,
        };
    }

    static QueryEntry BuildPropertyEntry(MemberAccessExpressionSyntax node, IPropertySymbol prop, SemanticModel model)
    {
        var isWrite = IsWriteTarget(node);
        var receiverType = model.GetTypeInfo(node.Expression).Type;
        var typeName = receiverType?.ToDisplayString()
                       ?? prop.ContainingType.ToDisplayString();
        return new QueryEntry
        {
            Kind = isWrite ? UsageKind.PropertyWrite : UsageKind.PropertyRead,
            TypeName = typeName,
            PropertyName = prop.Name,
        };
    }

    static QueryEntry BuildFieldEntry(MemberAccessExpressionSyntax node, IFieldSymbol field, SemanticModel model)
    {
        var isWrite = IsWriteTarget(node);
        var receiverType = model.GetTypeInfo(node.Expression).Type;
        var typeName = receiverType?.ToDisplayString()
                       ?? field.ContainingType.ToDisplayString();
        return new QueryEntry
        {
            Kind = isWrite ? UsageKind.FieldWrite : UsageKind.FieldRead,
            TypeName = typeName,
            FieldName = field.Name,
        };
    }

    // ── Identifier (simple name — property/field without explicit receiver) ───

    static QueryEntry? FromIdentifier(IdentifierNameSyntax node, SemanticModel model)
    {
        return TryFromTypeArgument(node, model)
            ?? TryFromObjectInitializerLeft(node, model)
            ?? TryFromObjectInitializerRight(node, model)
            ?? TryFromSymbol(node, model);
    }

    static QueryEntry? TryFromTypeArgument(IdentifierNameSyntax node, SemanticModel model)
    {
        if (node.Parent is not TypeArgumentListSyntax) return null;
        var typeSymbol = model.GetTypeInfo(node).Type;
        if (typeSymbol is null) return null;
        return new QueryEntry { Kind = UsageKind.GenericArgument, TypeName = typeSymbol.ToDisplayString() };
    }

    static QueryEntry? TryFromObjectInitializerLeft(IdentifierNameSyntax node, SemanticModel model)
    {
        if (node.Parent is not AssignmentExpressionSyntax assign
            || assign.Left != node
            || assign.Parent is not InitializerExpressionSyntax) return null;

        return model.GetSymbolInfo(node).Symbol switch
        {
            IPropertySymbol p => new QueryEntry { Kind = UsageKind.PropertyWrite, TypeName = p.ContainingType.ToDisplayString(), PropertyName = p.Name },
            IFieldSymbol f    => new QueryEntry { Kind = UsageKind.FieldWrite,    TypeName = f.ContainingType.ToDisplayString(), FieldName = f.Name },
            _                 => null,
        };
    }

    static QueryEntry? TryFromObjectInitializerRight(IdentifierNameSyntax node, SemanticModel model)
    {
        if (node.Parent is not AssignmentExpressionSyntax assign
            || assign.Right != node
            || assign.Parent is not InitializerExpressionSyntax) return null;

        var typeSymbol = model.GetTypeInfo(node).Type;
        if (typeSymbol is null) return null;
        return new QueryEntry
        {
            Kind = UsageKind.PropertyWrite,
            TypeName = typeSymbol.ToDisplayString(),
            PropertyName = assign.Left is IdentifierNameSyntax lhs ? lhs.Identifier.Text : null,
        };
    }

    static QueryEntry? TryFromSymbol(IdentifierNameSyntax node, SemanticModel model)
    {
        if (IsDeclarationContext(node)) return null;

        if (node.Parent is MemberAccessExpressionSyntax ma && ma.Name == node) return null;
        if (node.Parent is InvocationExpressionSyntax or QualifiedNameSyntax) return null;

        return model.GetSymbolInfo(node).Symbol switch
        {
            IPropertySymbol prop => new QueryEntry
            {
                Kind = IsWriteTarget(node) ? UsageKind.PropertyWrite : UsageKind.PropertyRead,
                TypeName = prop.Type.ToDisplayString(),
                PropertyName = prop.Name,
            },
            IFieldSymbol field => new QueryEntry
            {
                Kind = IsWriteTarget(node) ? UsageKind.FieldWrite : UsageKind.FieldRead,
                TypeName = field.Type.ToDisplayString(),
                FieldName = field.Name,
            },
            _ => TryBuildReturnTypeEntry(node, model),
        };
    }

    // ── Generic name (type arguments) ─────────────────────────────────────────

    static QueryEntry? FromGenericName(GenericNameSyntax node, SemanticModel model)
    {
        if (node.Parent is TypeArgumentListSyntax)
        {
            var typeSymbol = model.GetTypeInfo(node).Type;
            if (typeSymbol is null) return null;
            return new QueryEntry
            {
                Kind = UsageKind.GenericArgument,
                TypeName = typeSymbol.ToDisplayString(),
            };
        }

        return null;
    }

    // ── Type constraint (where T : MyType) ────────────────────────────────────

    static QueryEntry? FromTypeConstraint(TypeConstraintSyntax node, SemanticModel model)
    {
        var typeSymbol = model.GetTypeInfo(node.Type).Type;
        if (typeSymbol is null) return null;

        return new QueryEntry
        {
            Kind = UsageKind.GenericConstraint,
            TypeName = typeSymbol.ToDisplayString(),
        };
    }

    // ── Parameter type ────────────────────────────────────────────────────────

    static QueryEntry? FromParameter(ParameterSyntax node, SemanticModel model)
    {
        if (node.Type is null) return null;
        var typeSymbol = model.GetTypeInfo(node.Type).Type;
        if (typeSymbol is null) return null;

        // Unwrap nullable: IRequest? → IRequest
        var namedType = typeSymbol is INamedTypeSymbol { NullableAnnotation: NullableAnnotation.Annotated } nullable
            ? nullable.TypeArguments.FirstOrDefault() ?? typeSymbol
            : typeSymbol;

        return new QueryEntry
        {
            Kind = UsageKind.Parameter,
            TypeName = namedType.ToDisplayString(),
        };
    }

    // ── Return type detection ─────────────────────────────────────────────────

    static QueryEntry? TryBuildReturnTypeEntry(IdentifierNameSyntax node, SemanticModel model)
    {
        var isReturnTypeContext = node.Parent switch
        {
            MethodDeclarationSyntax method                                                    => method.ReturnType == node,
            PropertyDeclarationSyntax prop                                                    => prop.Type == node,
            VariableDeclarationSyntax varDecl when varDecl.Parent is FieldDeclarationSyntax  => varDecl.Type == node,
            _                                                                                 => false,
        };

        return isReturnTypeContext ? BuildReturnTypeEntry(node, model) : null;
    }

    static QueryEntry? BuildReturnTypeEntry(IdentifierNameSyntax node, SemanticModel model)
    {
        var typeSymbol = model.GetTypeInfo(node).Type;
        if (typeSymbol is null) return null;
        return new QueryEntry
        {
            Kind = UsageKind.ReturnType,
            TypeName = typeSymbol.ToDisplayString(),
        };
    }

    // ── typeof ────────────────────────────────────────────────────────────────

    static QueryEntry? FromTypeOf(TypeOfExpressionSyntax node, SemanticModel model)
    {
        var typeSymbol = model.GetTypeInfo(node.Type).Type;
        if (typeSymbol is null) return null;

        return new QueryEntry
        {
            Kind = UsageKind.TypeOf,
            TypeName = typeSymbol.ToDisplayString(),
        };
    }

    // ── nameof ────────────────────────────────────────────────────────────────

    static QueryEntry? FromNameOf(InvocationExpressionSyntax node, SemanticModel model)
    {
        var arg = node.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        if (arg is null) return null;

        // nameof(Animal.Name) or nameof(Animal)
        if (arg is MemberAccessExpressionSyntax memberAccess)
        {
            var containingType = model.GetTypeInfo(memberAccess.Expression).Type;
            if (containingType is null) return null;

            var memberSymbol = model.GetSymbolInfo(memberAccess).Symbol;
            return new QueryEntry
            {
                Kind = UsageKind.NameOf,
                TypeName = containingType.ToDisplayString(),
                MethodName   = memberSymbol is IMethodSymbol   m ? m.Name : null,
                PropertyName = memberSymbol is IPropertySymbol p ? p.Name : null,
                FieldName    = memberSymbol is IFieldSymbol    f ? f.Name : null,
            };
        }

        // nameof(Animal) — type only, or nameof(GetThisMethodName) — member of current class
        // Note: for method groups, GetSymbolInfo returns no Symbol but CandidateSymbols has the method
        var symbolInfo = model.GetSymbolInfo(arg);
        var symbol = symbolInfo.Symbol
                  ?? symbolInfo.CandidateSymbols.FirstOrDefault();

        if (symbol is ITypeSymbol typeSymbol)
            return new QueryEntry { Kind = UsageKind.NameOf, TypeName = typeSymbol.ToDisplayString() };

        if (symbol is not null && symbol.ContainingType is not null)
            return new QueryEntry
            {
                Kind = UsageKind.NameOf,
                TypeName = symbol.ContainingType.ToDisplayString(),
                MethodName   = symbol is IMethodSymbol   m ? m.Name : null,
                PropertyName = symbol is IPropertySymbol p ? p.Name : null,
                FieldName    = symbol is IFieldSymbol    f ? f.Name : null,
            };

        return null;
    }

    // ── is / as ───────────────────────────────────────────────────────────────

    static QueryEntry? FromBinaryExpression(BinaryExpressionSyntax node, SemanticModel model)
    {
        if (!node.IsKind(SyntaxKind.IsExpression) && !node.IsKind(SyntaxKind.AsExpression))
            return null;

        var typeSymbol = model.GetTypeInfo(node.Right).Type;
        if (typeSymbol is null) return null;

        return new QueryEntry
        {
            Kind = UsageKind.IsOrAs,
            TypeName = typeSymbol.ToDisplayString(),
        };
    }

    static QueryEntry? FromEventAssignment(AssignmentExpressionSyntax node, SemanticModel model)
    {
        var kind = node.Kind();
        if (kind is not SyntaxKind.AddAssignmentExpression and not SyntaxKind.SubtractAssignmentExpression)
            return null;

        if (model.GetSymbolInfo(node.Left).Symbol is not IEventSymbol eventSymbol)
            return null;

        var receiverType = node.Left is MemberAccessExpressionSyntax ma
            ? model.GetTypeInfo(ma.Expression).Type?.ToDisplayString()
            : null;

        return new QueryEntry
        {
            Kind = kind == SyntaxKind.AddAssignmentExpression ? UsageKind.EventSubscribe : UsageKind.EventUnsubscribe,
            TypeName = receiverType ?? eventSymbol.ContainingType.ToDisplayString(),
            EventName = eventSymbol.Name,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static bool IsDeclarationContext(IdentifierNameSyntax node) => node.Parent switch
    {
        ExplicitInterfaceSpecifierSyntax => true,
        NameEqualsSyntax                 => true,
        _                                => false,
    };

    static bool IsWriteTarget(SyntaxNode node) => node.Parent switch
    {
        AssignmentExpressionSyntax assign when assign.Left == node                                    => true,
        AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax }                            => true,
        ArgumentSyntax arg when arg.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)                      => true,
        _                                                                                             => false,
    };

    static IReadOnlyList<string> GetArgumentTypes(ArgumentListSyntax? argList, SemanticModel model)
    {
        if (argList is null) return [];

        var result = new List<string>(argList.Arguments.Count);
        foreach (var arg in argList.Arguments)
        {
            var typeInfo = model.GetTypeInfo(arg.Expression);
            result.Add(typeInfo.Type?.ToDisplayString() ?? "?");
        }
        return result;
    }
}
