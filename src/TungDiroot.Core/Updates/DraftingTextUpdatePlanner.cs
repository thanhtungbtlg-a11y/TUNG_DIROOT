using System;
using System.Collections.Generic;
using System.Linq;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Layout;

namespace TungDiroot.Core.Updates;

public static class DraftingTextUpdatePlanner
{
    public static UpdatePlan Create(WorkbookRangeModel after, IEnumerable<KeyValuePair<string, string>> taggedTexts)
    {
        if (after == null) throw new ArgumentNullException(nameof(after));
        if (taggedTexts == null) throw new ArgumentNullException(nameof(taggedTexts));
        var notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in taggedTexts)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || notes.ContainsKey(item.Key))
                throw ScopeChanged("Tag TextNote trống hoặc bị lặp / Empty or duplicate text tag: " + item.Key);
            notes.Add(item.Key, item.Value ?? string.Empty);
        }

        var renderedTexts = TableLayoutPlanner.Create(after, LayoutOptions.Default).Texts;
        if (notes.Count != renderedTexts.Count || renderedTexts.Any(text => !notes.ContainsKey(text.CellKey)))
            throw ScopeChanged("Phạm vi TextNote không khớp text được render / Linked text scope does not match rendered cells");

        var changes = renderedTexts.Where(text => !string.Equals(notes[text.CellKey], text.Text, StringComparison.Ordinal))
            .Select(text => new CellValueChange(text.CellKey, notes[text.CellKey], text.Text)).ToArray();
        return new UpdatePlan(UpdateAction.DataOnly, after, changes, null);
    }

    private static InvalidOperationException ScopeChanged(string detail) => new(
        "DATA_ONLY_TEXT_SCOPE_CHANGED: " + detail + ". DataOnly không thêm/xóa TextNote. " +
        "Hãy chọn Dựng lại bảng (Recreate); chỉnh sửa thủ công có thể mất. / " +
        "Choose Recreate explicitly to rebuild the table; manual edits may be lost.");
}
