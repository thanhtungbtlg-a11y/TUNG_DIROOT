using System;
using System.Collections.Generic;
using System.Linq;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Import;
using TungDiroot.Core.Layout;

namespace TungDiroot.Core.Preview;

public static class WorkbookPreviewAnalyzer
{
    private const int LargeRevitTableCellCount = 5_000;
    private static readonly IReadOnlyDictionary<string, string> FormulaErrorCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["#REF!"] = "FORMULA_REF_ERROR",
            ["#VALUE!"] = "FORMULA_VALUE_ERROR",
            ["#DIV/0!"] = "FORMULA_DIV_ZERO",
            ["#NAME?"] = "FORMULA_NAME_ERROR",
            ["#NUM!"] = "FORMULA_NUMBER_ERROR",
            ["#N/A"] = "FORMULA_NOT_AVAILABLE",
            ["#NULL!"] = "FORMULA_NULL_ERROR"
        };

    public static WorkbookPreviewReport Analyze(WorkbookRangeModel model, ImportMode mode)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        var issues = new List<WorkbookPreviewIssue>();

        foreach (var warning in model.Warnings)
            AddDistinct(issues, new WorkbookPreviewIssue(
                PreviewIssueSeverity.Warning,
                warning.Code,
                warning.Message));

        foreach (var cell in model.Cells)
        {
            if (!FormulaErrorCodes.TryGetValue(cell.DisplayText.Trim(), out var code)) continue;
            AddDistinct(issues, new WorkbookPreviewIssue(
                PreviewIssueSeverity.Error,
                code,
                $"Ô {cell.Address.ToA1()} có lỗi công thức {cell.DisplayText}.",
                cell.Address.ToA1()));
        }

        foreach (var drawing in model.Drawings.Items)
        {
            if (drawing.Kind == WorkbookDrawingKind.Image)
            {
                if (drawing.ImageBytes.IsEmpty)
                    AddDistinct(issues, new WorkbookPreviewIssue(
                        PreviewIssueSeverity.Warning,
                        "IMAGE_DATA_MISSING",
                        $"Không đọc được dữ liệu ảnh tại {drawing.Anchor}."));
                continue;
            }

            if (drawing.CachedPreview == null)
                AddDistinct(issues, new WorkbookPreviewIssue(
                    PreviewIssueSeverity.Warning,
                    "DRAWING_PREVIEW_MISSING",
                    $"{drawing.Kind} tại {drawing.Anchor} không có ảnh xem trước và có thể không hiển thị đúng."));
        }

        if (mode == ImportMode.RevitTable && model.Cells.Count > LargeRevitTableCellCount)
            issues.Add(new WorkbookPreviewIssue(
                PreviewIssueSeverity.Warning,
                "LARGE_REVIT_TABLE",
                $"Vùng có {model.Cells.Count} ô; bảng Revit có thể tạo nhiều element và xử lý chậm."));

        if (model.MergedRanges.Count > 0)
            issues.Add(new WorkbookPreviewIssue(
                PreviewIssueSeverity.Information,
                "MERGED_CELLS",
                $"Vùng có {model.MergedRanges.Count} vùng ô gộp; hãy kiểm tra căn lề trong preview."));

        var estimate = Estimate(model, mode);
        var ordered = issues
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.CellAddress, StringComparer.OrdinalIgnoreCase)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ToArray();
        return new WorkbookPreviewReport(
            model.Rows.Count,
            model.Columns.Count,
            model.Cells.Count,
            model.MergedRanges.Count,
            estimate,
            ordered);
    }

    private static WorkbookElementEstimate Estimate(WorkbookRangeModel model, ImportMode mode)
    {
        if (mode == ImportMode.Image)
            return new WorkbookElementEstimate(0, 0, 0, 2);

        var layout = TableLayoutPlanner.Create(model, LayoutOptions.Default);
        return new WorkbookElementEstimate(
            layout.Texts.Count,
            layout.Borders.Count,
            layout.Fills.Count,
            0);
    }

    private static void AddDistinct(ICollection<WorkbookPreviewIssue> issues, WorkbookPreviewIssue candidate)
    {
        if (issues.Any(issue => string.Equals(issue.Code, candidate.Code, StringComparison.Ordinal) &&
            string.Equals(issue.Message, candidate.Message, StringComparison.Ordinal))) return;
        issues.Add(candidate);
    }
}

