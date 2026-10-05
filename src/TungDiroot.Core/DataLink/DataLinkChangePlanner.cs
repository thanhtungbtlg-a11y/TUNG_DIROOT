using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TungDiroot.Core.DataLink;

public static class DataLinkChangePlanner
{
    public static DataLinkChangePlan Create(
        DataLinkWorkbook workbook,
        DataLinkDocumentStamp activeDocument,
        IReadOnlyDictionary<DataLinkValueAddress, DataLinkCurrentValue> currentValues)
    {
        if (workbook == null) throw new ArgumentNullException(nameof(workbook));
        if (activeDocument == null) throw new ArgumentNullException(nameof(activeDocument));
        if (currentValues == null) throw new ArgumentNullException(nameof(currentValues));

        var changes = workbook.Rows.Select(row => CreateCandidate(workbook, activeDocument, currentValues, row)).ToArray();
        MarkTypeConflicts(changes);
        return new DataLinkChangePlan(activeDocument, changes);
    }

    private static DataLinkCandidateChange CreateCandidate(
        DataLinkWorkbook workbook,
        DataLinkDocumentStamp activeDocument,
        IReadOnlyDictionary<DataLinkValueAddress, DataLinkCurrentValue> currentValues,
        DataLinkElementRow row)
    {
        if (!string.Equals(workbook.DocumentStamp.Identity, activeDocument.Identity, StringComparison.Ordinal))
            return Blocked(row, DataLinkChangeStatus.DifferentDocument, "File Excel thuộc model khác.");

        if (!row.IncludeInImport)
            return new DataLinkCandidateChange(row, DataLinkChangeStatus.NoChange, null, "Đã bỏ chọn trong Excel.");

        if (!currentValues.TryGetValue(DataLinkValueAddress.From(row), out var current) || !current.ElementExists)
            return Blocked(row, DataLinkChangeStatus.ElementMissing, "Element không còn tồn tại.");

        if (!current.ParameterExists)
            return Blocked(row, DataLinkChangeStatus.ParameterMissing, "Parameter không còn tồn tại trên element.");

        if (current.IsReadOnly)
            return Blocked(row, DataLinkChangeStatus.ReadOnly, "Parameter chỉ đọc.");

        if (!current.IsEditable)
            return Blocked(row, DataLinkChangeStatus.NotEditable, "Element hoặc type chưa thể chỉnh sửa.");

        if (!IsSupportedStorage(row.Parameter.StorageKind) ||
            !string.Equals(row.Parameter.StorageKind, current.StorageKind, StringComparison.Ordinal))
            return Blocked(row, DataLinkChangeStatus.UnsupportedStorage, "Kiểu dữ liệu không được hỗ trợ hoặc đã thay đổi.");

        if (!string.Equals(current.InternalValue, row.OriginalInternal, StringComparison.Ordinal))
            return Blocked(row, DataLinkChangeStatus.StaleModelValue, "Giá trị trong Revit đã thay đổi sau khi xuất.");

        if (!TryParse(row.Parameter.StorageKind, row.EditedDisplay, workbook.CultureName, out var parsed))
            return Blocked(row, DataLinkChangeStatus.InvalidValue, "Giá trị Excel không đúng kiểu dữ liệu.");

        if (string.Equals(parsed, row.OriginalInternal, StringComparison.Ordinal))
            return new DataLinkCandidateChange(row, DataLinkChangeStatus.NoChange, parsed, "Không thay đổi.");

        return new DataLinkCandidateChange(row, DataLinkChangeStatus.Ready, parsed, "Sẵn sàng cập nhật.");
    }

    private static void MarkTypeConflicts(DataLinkCandidateChange[] changes)
    {
        var groups = changes
            .Select((change, index) => new { Change = change, Index = index })
            .Where(item => item.Change.Row.Parameter.IsType && !string.IsNullOrWhiteSpace(item.Change.Row.TypeUniqueId))
            .Where(item => item.Change.Status == DataLinkChangeStatus.Ready || item.Change.Status == DataLinkChangeStatus.NoChange)
            .GroupBy(item => new { item.Change.Row.TypeUniqueId, item.Change.Row.Parameter.StableId });

        foreach (var group in groups)
        {
            if (group.Select(item => item.Change.ParsedInternal).Distinct(StringComparer.Ordinal).Count() <= 1) continue;
            foreach (var item in group)
            {
                changes[item.Index] = Blocked(item.Change.Row, DataLinkChangeStatus.TypeConflict,
                    "Các element cùng Type đang yêu cầu nhiều giá trị khác nhau.");
            }
        }
    }

    private static DataLinkCandidateChange Blocked(
        DataLinkElementRow row,
        DataLinkChangeStatus status,
        string message) => new(row, status, null, message);

    private static bool IsSupportedStorage(string storageKind) =>
        string.Equals(storageKind, "String", StringComparison.Ordinal) ||
        string.Equals(storageKind, "Integer", StringComparison.Ordinal) ||
        string.Equals(storageKind, "Double", StringComparison.Ordinal);

    private static bool TryParse(string storageKind, string text, string cultureName, out string parsed)
    {
        parsed = string.Empty;
        if (string.Equals(storageKind, "String", StringComparison.Ordinal))
        {
            parsed = text ?? string.Empty;
            return true;
        }

        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }

        if (string.Equals(storageKind, "Integer", StringComparison.Ordinal))
        {
            if (!int.TryParse(text, NumberStyles.Integer, culture, out var integer)) return false;
            parsed = integer.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, culture, out var number) ||
            double.IsNaN(number) || double.IsInfinity(number)) return false;
        parsed = number.ToString("R", CultureInfo.InvariantCulture);
        return true;
    }
}
