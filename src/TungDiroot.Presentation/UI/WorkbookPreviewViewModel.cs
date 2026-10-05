using System;
using System.Collections.Generic;
using System.Linq;
using TungDiroot.Core.Preview;

namespace TungDiroot.Presentation.UI;

public sealed record WorkbookPreviewIssueRow(
    string SeverityText,
    string Code,
    string Message,
    string CellAddress);

public sealed class WorkbookPreviewViewModel
{
    public WorkbookPreviewViewModel(string sourceLabel, WorkbookPreviewReport report)
    {
        SourceLabel = sourceLabel ?? throw new ArgumentNullException(nameof(sourceLabel));
        Report = report ?? throw new ArgumentNullException(nameof(report));
        Issues = report.Issues.Select(issue => new WorkbookPreviewIssueRow(
            issue.Severity.ToString(),
            issue.Code,
            issue.Message,
            issue.CellAddress ?? string.Empty)).ToArray();
    }

    public string SourceLabel { get; }
    public WorkbookPreviewReport Report { get; }
    public IReadOnlyList<WorkbookPreviewIssueRow> Issues { get; }
    public bool CanImport => Report.CanImport;
    public string DimensionsText => $"{Report.RowCount} hàng × {Report.ColumnCount} cột · {Report.CellCount} ô";
    public string EstimateText => $"Ước tính {Report.Estimate.TotalElements} element Revit";
    public string StatusText => Report.Issues.Any(issue => issue.Severity == PreviewIssueSeverity.Error)
        ? "Có lỗi cần sửa"
        : Report.Issues.Any(issue => issue.Severity == PreviewIssueSeverity.Warning)
            ? "Có cảnh báo"
            : "Sẵn sàng";
}

