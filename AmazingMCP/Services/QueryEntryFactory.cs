using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services;

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
        var symbol = model.GetSymbolInfo(node).Symbol as IMethodSymbol;
        if (symbol is null) return null;

        // Skip if this is not a member access — no explicit receiver
        if (node.Expression is not MemberAccessExpressionSyntax memberAccess)
            return null;

        // Use the receiver's actual type so that e.g. ILogger<T> is preserved
        // even when the method is declared on a base type or as an extension method.
        var receiverType = model.GetTypeInfo(memberAccess.Expression).Type;
        var typeName = receiverType?.ToDisplayString()
                       ?? symbol.ContainingType.ToDisplayString();

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
        return new QueryEntry { Kind = UsageKind.TypeAsGenericArgument, TypeName = typeSymbol.ToDisplayString() };
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
            IPropertySymbol or IFieldSymbol => null,
            _                               => TryBuildReturnTypeEntry(node, model),
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
                Kind = UsageKind.TypeAsGenericArgument,
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
            Kind = UsageKind.TypeAsGenericConstraint,
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
            Kind = UsageKind.TypeAsParameter,
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
            Kind = UsageKind.TypeAsReturnType,
            TypeName = typeSymbol.ToDisplayString(),
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
