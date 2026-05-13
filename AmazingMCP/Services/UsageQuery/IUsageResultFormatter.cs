using AmazingMCP.Models.UsageQuery;

namespace AmazingMCP.Services.UsageQuery;

public interface IUsageResultFormatter
{
    string Format(IReadOnlyList<UsageMatch> matches, bool truncated = false);
}
