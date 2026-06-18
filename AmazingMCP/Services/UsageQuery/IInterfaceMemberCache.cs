using Microsoft.CodeAnalysis;

namespace AmazingMCP.Services.UsageQuery;

interface IInterfaceMemberCache
{
    IReadOnlyList<ISymbol> GetInterfaceMembers(ISymbol memberSymbol);
}
