namespace TungDiroot.Core.Excel;

public sealed record RangeReadOptions(bool IncludeHiddenRowsAndColumns)
{
    public static RangeReadOptions Default { get; } = new RangeReadOptions(true);
}
