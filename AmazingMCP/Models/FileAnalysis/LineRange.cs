namespace AmazingMCP.Models.FileAnalysis;

/// <summary>1-based inclusive line range.</summary>
public readonly record struct LineRange(int Start, int End)
{
    public bool Contains(int line) => line >= Start && line <= End;
    public bool Overlaps(LineRange other) => Start <= other.End && other.Start <= End;
    public LineRange MergeWith(LineRange other) => new(Math.Min(Start, other.Start), Math.Max(End, other.End));
    public int Count => End - Start + 1;
}
