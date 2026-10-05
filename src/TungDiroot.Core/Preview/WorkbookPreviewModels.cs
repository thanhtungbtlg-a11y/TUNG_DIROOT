using System;
using System.Collections.Generic;
using System.Linq;

namespace TungDiroot.Core.Preview;

public enum PreviewIssueSeverity
{
    Information,
    Warning,
    Error
}

public sealed record WorkbookPreviewIssue(
    PreviewIssueSeverity Severity,
    string Code,
    string Message,
    string? CellAddress = null);

public sealed record WorkbookElementEstimate(
    int TextNotes,
    int BorderLines,
    int FilledRegions,
    int ImageElements)
{
    public int TotalElements => checked(TextNotes + BorderLines + FilledRegions + ImageElements);
}

public sealed class WorkbookPreviewReport
{
    public WorkbookPreviewReport(
        int rowCount,
        int columnCount,
        int cellCount,
        int mergedRangeCount,
        WorkbookElementEstimate estimate,
        IEnumerable<WorkbookPreviewIssue> issues)
    {
        if (rowCount < 0) throw new ArgumentOutOfRangeException(nameof(rowCount));
        if (columnCount < 0) throw new ArgumentOutOfRangeException(nameof(columnCount));
        if (cellCount < 0) throw new ArgumentOutOfRangeException(nameof(cellCount));
        if (mergedRangeCount < 0) throw new ArgumentOutOfRangeException(nameof(mergedRangeCount));
        RowCount = rowCount;
        ColumnCount = columnCount;
        CellCount = cellCount;
        MergedRangeCount = mergedRangeCount;
        Estimate = estimate ?? throw new ArgumentNullException(nameof(estimate));
        Issues = (issues ?? throw new ArgumentNullException(nameof(issues))).ToArray();
    }

    public int RowCount { get; }
    public int ColumnCount { get; }
    public int CellCount { get; }
    public int MergedRangeCount { get; }
    public WorkbookElementEstimate Estimate { get; }
    public IReadOnlyList<WorkbookPreviewIssue> Issues { get; }
    public bool CanImport => Issues.All(issue => issue.Severity != PreviewIssueSeverity.Error);
}

