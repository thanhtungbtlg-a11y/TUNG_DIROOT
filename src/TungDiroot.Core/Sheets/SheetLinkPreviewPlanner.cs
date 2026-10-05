using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TungDiroot.Core.SheetTools;

public sealed record SheetLinkExistingItem(string UniqueId, string Value);

public sealed record SheetLinkReferenceCatalog(
    IReadOnlyList<SheetLinkExistingItem> Sheets,
    IReadOnlyList<SheetLinkExistingItem> Views,
    IReadOnlyList<string> TitleBlockTypes,
    IReadOnlyList<int> RevisionSequences,
    IReadOnlyList<string> Levels,
    IReadOnlyList<string> ViewTemplates,
    IReadOnlyList<SheetLinkSheetRow>? CurrentSheets = null,
    IReadOnlyList<SheetLinkViewRow>? CurrentViews = null,
    IReadOnlyList<SheetLinkRevisionRow>? CurrentRevisions = null);

public enum SheetLinkPreviewStatus
{
    Ready,
    NoChange,
    Skipped,
    Invalid,
    Conflict
}

public enum SheetLinkRowKind
{
    Sheet,
    View,
    Revision
}

public sealed record SheetLinkPreviewItem(
    SheetLinkRowKind Kind,
    int ExcelRow,
    SheetLinkAction Action,
    string Key,
    SheetLinkPreviewStatus Status,
    string Code,
    string Message);

public sealed record SheetLinkPreviewPlan(IReadOnlyList<SheetLinkPreviewItem> Items)
{
    public string RevisionContextStamp { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> ExistingRevisionAliases { get; init; } = new Dictionary<string, string>();
    public int ReadyCount => Items.Count(item => item.Status == SheetLinkPreviewStatus.Ready);
    public bool HasErrors => Items.Any(item => item.Status is SheetLinkPreviewStatus.Invalid or SheetLinkPreviewStatus.Conflict);
}

public static class SheetLinkPreviewPlanner
{
    private static readonly StringComparer Names = StringComparer.OrdinalIgnoreCase;

    public static SheetLinkPreviewPlan Plan(SheetLinkWorkbook workbook, SheetLinkReferenceCatalog catalog)
    {
        if (workbook == null) throw new ArgumentNullException(nameof(workbook));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));

        var items = new List<SheetLinkPreviewItem>();
        var revisionKeys = Duplicates(workbook.Revisions.Select(row => row.RevisionKey));
        var revisionIds = Duplicates(workbook.Revisions.Where(IsActive).Select(row => row.UniqueId ?? string.Empty));
        var revisionDescriptions = Duplicates(workbook.Revisions.Where(IsActive).Select(RevisionDescriptionKey));
        for (var index = 0; index < workbook.Revisions.Count; index++)
            items.Add(ValidateRevision(workbook.Revisions[index], workbook.SourceRow(SheetLinkRowKind.Revision, index), catalog, revisionKeys, revisionIds, revisionDescriptions));
        var availableRevisionKeys = new Dictionary<string, string>(Names);
        var existingRevisionAliases = new Dictionary<string, string>(Names);
        for (var index = 0; index < workbook.Revisions.Count; index++)
        {
            var row = workbook.Revisions[index];
            if (string.IsNullOrWhiteSpace(row.RevisionKey) || revisionKeys.Contains(row.RevisionKey.Trim())) continue;
            var current = catalog.CurrentRevisions?.FirstOrDefault(item => Names.Equals(item.UniqueId, row.UniqueId));
            // A CREATE with a forged existing id must never become an alias, even when that id resolves.
            var aliasValidation = row.Action is SheetLinkAction.Update or SheetLinkAction.Skip
                ? IsActive(row) ? items[index] : ValidateRevision(row with { Apply = true, Action = SheetLinkAction.Update },
                    workbook.SourceRow(SheetLinkRowKind.Revision, index), catalog, revisionKeys, revisionIds, revisionDescriptions)
                : null;
            var aliasAllowed = aliasValidation?.Status is SheetLinkPreviewStatus.Ready or SheetLinkPreviewStatus.NoChange;
            if (row.Action == SheetLinkAction.Skip) aliasAllowed = aliasValidation?.Status == SheetLinkPreviewStatus.NoChange;
            if (current != null && aliasAllowed)
            {
                availableRevisionKeys[row.RevisionKey.Trim()] = current.SequenceNumber?.ToString(CultureInfo.InvariantCulture) ?? "key:" + row.RevisionKey.Trim();
                existingRevisionAliases[row.RevisionKey.Trim()] = current.UniqueId!;
            }
            else if (row.Action == SheetLinkAction.Create && items[index].Status == SheetLinkPreviewStatus.Ready)
                availableRevisionKeys[row.RevisionKey.Trim()] = "key:" + row.RevisionKey.Trim();
        }
        var duplicateSheetNumbers = Duplicates(workbook.Sheets.Where(IsActive).Select(row => row.SheetNumber));
        var duplicateViewNames = Duplicates(workbook.Views.Where(IsActive).Select(row => row.ViewName));
        var duplicateSheetIds = Duplicates(workbook.Sheets.Where(row => IsActive(row) && row.Action == SheetLinkAction.Update)
            .Select(row => row.UniqueId ?? string.Empty));
        var duplicateViewIds = Duplicates(workbook.Views.Where(row => IsActive(row) && row.Action == SheetLinkAction.Update)
            .Select(row => row.UniqueId ?? string.Empty));
        var availableSheetNumbers = new HashSet<string>(catalog.Sheets.Select(item => item.Value), Names);
        foreach (var row in workbook.Sheets.Where(IsActive))
            if (!string.IsNullOrWhiteSpace(row.SheetNumber)) availableSheetNumbers.Add(row.SheetNumber.Trim());

        for (var index = 0; index < workbook.Sheets.Count; index++)
            items.Add(ValidateSheet(workbook.Sheets[index], workbook.SourceRow(SheetLinkRowKind.Sheet, index), catalog, duplicateSheetNumbers, duplicateSheetIds, availableRevisionKeys));
        for (var index = 0; index < workbook.Views.Count; index++)
            items.Add(ValidateView(workbook.Views[index], workbook.SourceRow(SheetLinkRowKind.View, index), catalog, duplicateViewNames, duplicateViewIds, availableSheetNumbers));
        return new SheetLinkPreviewPlan(items)
        {
            ExistingRevisionAliases = existingRevisionAliases,
            // Numeric revision references must not silently resolve to a different identity after deletion/reordering.
            RevisionContextStamp = string.Concat((catalog.CurrentRevisions ?? Array.Empty<SheetLinkRevisionRow>())
                .OrderBy(row => row.UniqueId, Names).Select(row => string.Concat(new[]
                {
                    row.UniqueId, row.SequenceNumber?.ToString(CultureInfo.InvariantCulture), row.Description, row.RevisionDate,
                    row.Issued.ToString(), row.IssuedBy, row.IssuedTo, row.Visibility.ToString()
                }.Select(StampPart))))
        };
    }

    public static void EnsurePreviewStillCurrent(SheetLinkPreviewPlan reviewed, SheetLinkPreviewPlan current)
    {
        if (!reviewed.Items.SequenceEqual(current.Items) || reviewed.RevisionContextStamp != current.RevisionContextStamp)
            throw new InvalidOperationException("SHEETLINK_PREVIEW_STALE: Model đã thay đổi. Hãy Preview lại trước khi Apply.");
    }

    public static IReadOnlyList<SheetLinkRevisionReference> ParseRevisionReferences(string value)
    {
        var result = new List<SheetLinkRevisionReference>();
        foreach (var raw in (value ?? string.Empty).Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            SheetLinkRevisionReference reference;
            if (token.StartsWith("key:", StringComparison.OrdinalIgnoreCase) && token.Substring(4).Trim().Length > 0)
                reference = new SheetLinkRevisionReference(null, token.Substring(4).Trim());
            else if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence) && sequence > 0)
                reference = new SheetLinkRevisionReference(sequence, null);
            else throw new FormatException("Revision reference không hợp lệ: " + token);
            if (!result.Any(item => item.SequenceNumber == reference.SequenceNumber && Names.Equals(item.RevisionKey, reference.RevisionKey)))
                result.Add(reference);
        }
        return result;
    }

    public static IReadOnlyList<int> ParseRevisionSequences(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<int>();
        var result = new List<int>();
        foreach (var token in value.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(token.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence) || sequence <= 0)
                throw new FormatException("Revision sequence không hợp lệ: " + token.Trim());
            if (!result.Contains(sequence)) result.Add(sequence);
        }
        return result;
    }

    private static SheetLinkPreviewItem ValidateSheet(
        SheetLinkSheetRow row,
        int excelRow,
        SheetLinkReferenceCatalog catalog,
        HashSet<string> duplicates,
        HashSet<string> duplicateIds,
        IReadOnlyDictionary<string, string> revisionKeys)
    {
        if (!IsActive(row)) return Skipped(SheetLinkRowKind.Sheet, excelRow, row.Action, row.SheetNumber);
        var errors = new List<(string Code, string Message, bool Conflict)>();
        var number = row.SheetNumber.Trim();
        if (number.Length == 0) errors.Add(("SHEET_NUMBER_REQUIRED", "Thiếu SHEET_NUMBER.", false));
        if (string.IsNullOrWhiteSpace(row.SheetName)) errors.Add(("SHEET_NAME_REQUIRED", "Thiếu SHEET_NAME.", false));
        if (string.IsNullOrWhiteSpace(row.TitleBlockType))
            errors.Add(("TITLE_BLOCK_REQUIRED", "Thiếu TITLE_BLOCK_TYPE.", false));
        else if (!catalog.TitleBlockTypes.Contains(row.TitleBlockType.Trim(), Names))
            errors.Add(("TITLE_BLOCK_NOT_FOUND", "Không tìm thấy Title Block Type '" + row.TitleBlockType.Trim() + "'.", false));

        if (row.Action == SheetLinkAction.Update)
        {
            if (duplicateIds.Contains(row.UniqueId ?? string.Empty))
                errors.Add(("SHEET_UPDATE_ID_DUPLICATE", "Nhiều dòng UPDATE cùng UNIQUE_ID của Sheet.", true));
            if (string.IsNullOrWhiteSpace(row.UniqueId) || !catalog.Sheets.Any(item => Names.Equals(item.UniqueId, row.UniqueId)))
                errors.Add(("SHEET_UPDATE_NOT_FOUND", "UNIQUE_ID của sheet UPDATE không tồn tại.", false));
        }

        if (duplicates.Contains(number))
            errors.Add(("SHEET_NUMBER_DUPLICATE", "SHEET_NUMBER bị lặp trong workbook.", true));
        var numberOwner = catalog.Sheets.FirstOrDefault(item => Names.Equals(item.Value, number));
        if (numberOwner != null && (row.Action == SheetLinkAction.Create || !Names.Equals(numberOwner.UniqueId, row.UniqueId)))
            errors.Add(("SHEET_NUMBER_DUPLICATE", "SHEET_NUMBER đã tồn tại trong model.", true));

        try
        {
            var references = ParseRevisionReferences(row.RevisionSequences);
            if (references.Any(reference => reference.SequenceNumber.HasValue && !catalog.RevisionSequences.Contains(reference.SequenceNumber.Value)))
                errors.Add(("REVISION_NOT_FOUND", "Có revision sequence không tồn tại trong model.", false));
            if (references.Any(reference => reference.RevisionKey != null && !revisionKeys.ContainsKey(reference.RevisionKey)))
                errors.Add(("REVISION_KEY_NOT_FOUND", "Có key revision không tồn tại hoặc dòng Revision không hợp lệ.", true));
        }
        catch (FormatException exception) { errors.Add(("REVISION_INVALID", exception.Message, false)); }

        var changes = new List<string>();
        var current = catalog.CurrentSheets?.FirstOrDefault(item => Names.Equals(item.UniqueId, row.UniqueId));
        if (row.Action == SheetLinkAction.Update && current != null)
        {
            Change(changes, "SHEET_NUMBER", current.SheetNumber, row.SheetNumber);
            Change(changes, "SHEET_NAME", current.SheetName, row.SheetName);
            Change(changes, "TITLE_BLOCK_TYPE", current.TitleBlockType, row.TitleBlockType);
            if (errors.Count == 0)
                Change(changes, "REVISION_SEQUENCES", CanonicalRevisions(current.RevisionSequences, revisionKeys), CanonicalRevisions(row.RevisionSequences, revisionKeys));
        }
        else if (row.Action == SheetLinkAction.Create)
            changes.Add("CREATE Sheet: " + row.SheetNumber + " / " + row.SheetName + "; TITLE_BLOCK_TYPE: " + row.TitleBlockType + "; REVISION_SEQUENCES: " + Display(row.RevisionSequences));
        return Result(SheetLinkRowKind.Sheet, excelRow, row.Action, number, errors, changes, current != null);
    }

    private static SheetLinkPreviewItem ValidateView(
        SheetLinkViewRow row,
        int excelRow,
        SheetLinkReferenceCatalog catalog,
        HashSet<string> duplicates,
        HashSet<string> duplicateIds,
        HashSet<string> availableSheetNumbers)
    {
        if (!IsActive(row)) return Skipped(SheetLinkRowKind.View, excelRow, row.Action, row.ViewName);
        var errors = new List<(string Code, string Message, bool Conflict)>();
        var name = row.ViewName.Trim();
        if (row.Action == SheetLinkAction.Update && duplicateIds.Contains(row.UniqueId ?? string.Empty))
            errors.Add(("VIEW_UPDATE_ID_DUPLICATE", "Nhiều dòng UPDATE cùng UNIQUE_ID của View.", true));
        if (name.Length == 0) errors.Add(("VIEW_NAME_REQUIRED", "Thiếu VIEW_NAME.", false));
        if (row.Action == SheetLinkAction.Update &&
            (string.IsNullOrWhiteSpace(row.UniqueId) || !catalog.Views.Any(item => Names.Equals(item.UniqueId, row.UniqueId))))
            errors.Add(("VIEW_UPDATE_NOT_FOUND", "UNIQUE_ID của view UPDATE không tồn tại.", false));
        var current = catalog.CurrentViews?.FirstOrDefault(item => Names.Equals(item.UniqueId, row.UniqueId));
        if (row.Action == SheetLinkAction.Update && current != null)
        {
            if (current.ViewKind != row.ViewKind)
                errors.Add(("VIEW_KIND_UPDATE_BLOCKED", "UPDATE không thể đổi VIEW_KIND; hãy dùng CREATE cho view mới.", true));
            if (!Names.Equals(current.LevelName.Trim(), row.LevelName.Trim()))
                errors.Add(("VIEW_LEVEL_UPDATE_BLOCKED", "UPDATE không thể đổi LEVEL_NAME; hãy dùng CREATE cho view mới.", true));
            if (!string.IsNullOrWhiteSpace(current.SheetNumber) && !Names.Equals(current.SheetNumber, row.SheetNumber))
                errors.Add(("VIEWPORT_SHEET_UPDATE_BLOCKED", "Không tự xóa hoặc chuyển viewport khỏi Sheet hiện tại.", true));
        }

        if (duplicates.Contains(name)) errors.Add(("VIEW_NAME_DUPLICATE", "VIEW_NAME bị lặp trong workbook.", true));
        var nameOwner = catalog.Views.FirstOrDefault(item => Names.Equals(item.Value, name));
        if (nameOwner != null && (row.Action == SheetLinkAction.Create || !Names.Equals(nameOwner.UniqueId, row.UniqueId)))
            errors.Add(("VIEW_NAME_DUPLICATE", "VIEW_NAME đã tồn tại trong model.", true));

        if (row.ViewKind is SheetLinkViewKind.FloorPlan or SheetLinkViewKind.CeilingPlan)
        {
            if (string.IsNullOrWhiteSpace(row.LevelName) || !catalog.Levels.Contains(row.LevelName.Trim(), Names))
                errors.Add(("LEVEL_NOT_FOUND", "Không tìm thấy LEVEL_NAME cho plan view.", false));
        }
        if (!string.IsNullOrWhiteSpace(row.ViewTemplate) && !catalog.ViewTemplates.Contains(row.ViewTemplate.Trim(), Names))
            errors.Add(("VIEW_TEMPLATE_NOT_FOUND", "Không tìm thấy VIEW_TEMPLATE.", false));
        if (!string.IsNullOrWhiteSpace(row.SheetNumber) && !availableSheetNumbers.Contains(row.SheetNumber.Trim()))
            errors.Add(("VIEWPORT_SHEET_NOT_FOUND", "Không tìm thấy SHEET_NUMBER để đặt viewport.", false));
        if (row.CenterXMillimeters.HasValue != row.CenterYMillimeters.HasValue)
            errors.Add(("VIEWPORT_COORDINATES_INCOMPLETE", "CENTER_X_MM và CENTER_Y_MM phải cùng có hoặc cùng để trống.", false));
        else if (!string.IsNullOrWhiteSpace(row.SheetNumber) && !row.CenterXMillimeters.HasValue)
            errors.Add(("VIEWPORT_COORDINATES_REQUIRED", "Đặt viewport lên sheet cần CENTER_X_MM và CENTER_Y_MM.", false));

        if ((row.CenterXMillimeters.HasValue && !Finite(row.CenterXMillimeters.Value)) ||
            (row.CenterYMillimeters.HasValue && !Finite(row.CenterYMillimeters.Value)))
            errors.Add(("VIEWPORT_COORDINATES_INVALID", "Tọa độ phải là số hữu hạn.", false));
        var changes = new List<string>();
        if (row.Action == SheetLinkAction.Update && current != null)
        {
            Change(changes, "VIEW_NAME", current.ViewName, row.ViewName);
            Change(changes, "VIEW_TEMPLATE", current.ViewTemplate, row.ViewTemplate);
            Change(changes, "SHEET_NUMBER", current.SheetNumber, row.SheetNumber);
            if (!string.IsNullOrWhiteSpace(row.SheetNumber))
            {
                Change(changes, "CENTER_X_MM", Number(current.CenterXMillimeters), Number(row.CenterXMillimeters));
                Change(changes, "CENTER_Y_MM", Number(current.CenterYMillimeters), Number(row.CenterYMillimeters));
            }
        }
        else if (row.Action == SheetLinkAction.Create)
            changes.Add("CREATE View: " + row.ViewName + "; VIEW_KIND: " + row.ViewKind + "; LEVEL_NAME: " + Display(row.LevelName) +
                "; VIEW_TEMPLATE: " + Display(row.ViewTemplate) + "; SHEET_NUMBER: " + Display(row.SheetNumber) +
                "; CENTER_X_MM: " + Display(Number(row.CenterXMillimeters)) + "; CENTER_Y_MM: " + Display(Number(row.CenterYMillimeters)));
        return Result(SheetLinkRowKind.View, excelRow, row.Action, name, errors, changes, current != null);
    }

    private static SheetLinkPreviewItem ValidateRevision(SheetLinkRevisionRow row, int excelRow, SheetLinkReferenceCatalog catalog,
        HashSet<string> duplicateKeys, HashSet<string> duplicateIds, HashSet<string> duplicateDescriptions)
    {
        if (!IsActive(row)) return Skipped(SheetLinkRowKind.Revision, excelRow, row.Action, row.RevisionKey);
        var errors = new List<(string Code, string Message, bool Conflict)>();
        if (string.IsNullOrWhiteSpace(row.RevisionKey)) errors.Add(("REVISION_KEY_REQUIRED", "Thiếu REVISION_KEY.", false));
        else if (row.RevisionKey.IndexOfAny(new[] { ';', ',', '|', ':' }) >= 0)
            errors.Add(("REVISION_KEY_INVALID", "REVISION_KEY không chứa ; , | :.", false));
        if (duplicateKeys.Contains(row.RevisionKey.Trim())) errors.Add(("REVISION_KEY_DUPLICATE", "REVISION_KEY bị lặp.", true));
        if (duplicateIds.Contains(row.UniqueId ?? string.Empty)) errors.Add(("REVISION_ID_DUPLICATE", "Revision UPDATE bị lặp UNIQUE_ID.", true));
        if (string.IsNullOrWhiteSpace(row.Description)) errors.Add(("REVISION_DESCRIPTION_REQUIRED", "Thiếu DESCRIPTION.", false));
        if (!Enum.IsDefined(typeof(SheetLinkRevisionVisibility), row.Visibility))
            errors.Add(("REVISION_VISIBILITY_INVALID", "VISIBILITY không hợp lệ.", false));
        var current = catalog.CurrentRevisions?.FirstOrDefault(item => Names.Equals(item.UniqueId, row.UniqueId));
        if (row.Action == SheetLinkAction.Update)
        {
            if (current == null) errors.Add(("REVISION_UPDATE_NOT_FOUND", "Revision UNIQUE_ID không tồn tại.", false));
            else if (row.SequenceNumber.HasValue && row.SequenceNumber != current.SequenceNumber)
                errors.Add(("REVISION_SEQUENCE_READ_ONLY", "SEQUENCE_NUMBER chỉ đọc, không thể đổi thứ tự revision qua workbook.", true));
        }
        else if (row.Action == SheetLinkAction.Create)
        {
            if (!string.IsNullOrWhiteSpace(row.UniqueId) || row.SequenceNumber.HasValue)
                errors.Add(("REVISION_CREATE_ID_NOT_EMPTY", "CREATE phải để trống UNIQUE_ID và SEQUENCE_NUMBER.", false));
            if (duplicateDescriptions.Contains(RevisionDescriptionKey(row)) ||
                catalog.CurrentRevisions?.Any(item => Names.Equals(RevisionDescriptionKey(item), RevisionDescriptionKey(row))) == true)
                errors.Add(("REVISION_ALREADY_EXISTS", "DESCRIPTION/REVISION_DATE đã tồn tại hoặc bị lặp; dùng UPDATE bằng UNIQUE_ID.", true));
        }
        var changes = new List<string>();
        if (current != null && row.Action == SheetLinkAction.Update)
        {
            Change(changes, "DESCRIPTION", current.Description, row.Description);
            Change(changes, "REVISION_DATE", current.RevisionDate, row.RevisionDate);
            Change(changes, "ISSUED_BY", current.IssuedBy, row.IssuedBy);
            Change(changes, "ISSUED_TO", current.IssuedTo, row.IssuedTo);
            Change(changes, "VISIBILITY", current.Visibility.ToString(), row.Visibility.ToString());
            Change(changes, "ISSUED", current.Issued.ToString(), row.Issued.ToString());
            if (current.Issued && changes.Count > 0)
                errors.Add(("REVISION_ISSUED_READ_ONLY", "Revision đã Issued. Hãy xử lý/unissue trực tiếp trong Revit trước khi Preview lại.", true));
        }
        else if (row.Action == SheetLinkAction.Create)
            changes.Add("CREATE Revision key:" + row.RevisionKey + "; DESCRIPTION: " + row.Description + "; REVISION_DATE: " + Display(row.RevisionDate) +
                "; ISSUED: " + row.Issued + "; ISSUED_BY: " + Display(row.IssuedBy) + "; ISSUED_TO: " + Display(row.IssuedTo) + "; VISIBILITY: " + row.Visibility);
        return Result(SheetLinkRowKind.Revision, excelRow, row.Action, row.RevisionKey, errors, changes, current != null);
    }

    private static SheetLinkPreviewItem Result(
        SheetLinkRowKind kind,
        int excelRow,
        SheetLinkAction action,
        string key,
        IReadOnlyList<(string Code, string Message, bool Conflict)> errors,
        IReadOnlyList<string>? changes = null,
        bool hasSnapshot = false)
    {
        if (errors.Count == 0)
        {
            var unchanged = action == SheetLinkAction.Update && hasSnapshot && changes?.Count == 0;
            return new SheetLinkPreviewItem(kind, excelRow, action, key, unchanged ? SheetLinkPreviewStatus.NoChange : SheetLinkPreviewStatus.Ready,
                unchanged ? "NO_CHANGE" : "READY", unchanged ? "Không có thay đổi." : changes?.Count > 0 ? string.Join("; ", changes) : "Sẵn sàng.");
        }
        return new SheetLinkPreviewItem(kind, excelRow, action, key,
            errors.Any(error => error.Conflict) ? SheetLinkPreviewStatus.Conflict : SheetLinkPreviewStatus.Invalid,
            string.Join(";", errors.Select(error => error.Code).Distinct()),
            string.Join(" ", errors.Select(error => error.Message).Distinct()));
    }

    private static SheetLinkPreviewItem Skipped(SheetLinkRowKind kind, int row, SheetLinkAction action, string key) =>
        new(kind, row, action, key, SheetLinkPreviewStatus.Skipped, "SKIPPED", "Dòng không được chọn Apply.");

    private static bool IsActive(SheetLinkSheetRow row) => row.Apply && row.Action != SheetLinkAction.Skip;
    private static bool IsActive(SheetLinkViewRow row) => row.Apply && row.Action != SheetLinkAction.Skip;
    private static bool IsActive(SheetLinkRevisionRow row) => row.Apply && row.Action != SheetLinkAction.Skip;
    private static string RevisionDescriptionKey(SheetLinkRevisionRow row) => row.Description.Trim() + "\u001f" + row.RevisionDate.Trim();
    private static void Change(ICollection<string> changes, string field, string? before, string? after)
    {
        if (!string.Equals(before?.Trim(), after?.Trim(), StringComparison.Ordinal))
            changes.Add(field + ": " + Display(before) + " → " + Display(after));
    }
    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "<empty>" : value!.Trim();
    private static string StampPart(string? value) => (value?.Length ?? 0).ToString(CultureInfo.InvariantCulture) + ":" + value;
    private static string Number(double? value) => value?.ToString("0.######", CultureInfo.InvariantCulture) ?? string.Empty;
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static string CanonicalRevisions(string value, IReadOnlyDictionary<string, string> keys) => string.Join(";",
        ParseRevisionReferences(value).Select(reference => reference.SequenceNumber?.ToString(CultureInfo.InvariantCulture) ??
            (keys.TryGetValue(reference.RevisionKey!, out var resolved) ? resolved : "key:" + reference.RevisionKey))
            .Distinct(Names).OrderBy(item => int.TryParse(item, out var number) ? number : int.MaxValue).ThenBy(item => item, Names));

    private static HashSet<string> Duplicates(IEnumerable<string> values) => new(
        values.Select(value => value?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .GroupBy(value => value, Names)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key), Names);
}
