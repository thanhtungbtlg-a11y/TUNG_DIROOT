using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TungDiroot.Core.Parameters;

namespace TungDiroot.Presentation.UI;

public enum ParameterPlanStatusFilter
{
    All,
    Ready,
    AlreadyBound,
    ReviewRequired,
    MissingDefinition,
    InvalidCategory,
    ConfigurationError,
    AssignmentFailed
}

public sealed record ParameterPlanStatusFilterOption(ParameterPlanStatusFilter Value, string Label);

public static class ParameterPlanStatusDisplay
{
    public static IReadOnlyList<ParameterPlanStatusFilterOption> FilterOptions { get; } = new[]
    {
        new ParameterPlanStatusFilterOption(ParameterPlanStatusFilter.All, "Tất cả"),
        new ParameterPlanStatusFilterOption(ParameterPlanStatusFilter.Ready, "Sẵn sàng"),
        new ParameterPlanStatusFilterOption(ParameterPlanStatusFilter.AlreadyBound, "Đã gán"),
        new ParameterPlanStatusFilterOption(ParameterPlanStatusFilter.ReviewRequired, "Cần xem xét"),
        new ParameterPlanStatusFilterOption(ParameterPlanStatusFilter.MissingDefinition, "Thiếu definition"),
        new ParameterPlanStatusFilterOption(ParameterPlanStatusFilter.InvalidCategory, "Category không hợp lệ"),
        new ParameterPlanStatusFilterOption(ParameterPlanStatusFilter.ConfigurationError, "Cấu hình lỗi"),
        new ParameterPlanStatusFilterOption(ParameterPlanStatusFilter.AssignmentFailed, "Gán thất bại")
    };

    public static string For(ParameterPlanStatus status) => status switch
    {
        ParameterPlanStatus.Ready => "Sẵn sàng",
        ParameterPlanStatus.AlreadyBound => "Đã gán",
        ParameterPlanStatus.ReviewRequired => "Cần xem xét",
        ParameterPlanStatus.MissingDefinition => "Thiếu definition",
        ParameterPlanStatus.InvalidCategory => "Category không hợp lệ",
        ParameterPlanStatus.ConfigurationError => "Cấu hình lỗi",
        ParameterPlanStatus.AssignmentFailed => "Gán thất bại",
        _ => status.ToString()
    };
}

public static class ParameterPlanFilter
{
    public static void Apply(
        IEnumerable<ParameterPlanRowViewModel> rows,
        string searchText,
        ParameterPlanStatusFilter statusFilter,
        ObservableCollection<ParameterPlanRowViewModel> target)
    {
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        if (target == null) throw new ArgumentNullException(nameof(target));
        var query = (searchText ?? string.Empty).Trim();
        var filtered = rows.Where(row => MatchesStatus(row.Status, statusFilter) && MatchesSearch(row, query)).ToArray();
        target.Clear();
        foreach (var row in filtered) target.Add(row);
    }

    private static bool MatchesStatus(ParameterPlanStatus status, ParameterPlanStatusFilter filter) =>
        filter == ParameterPlanStatusFilter.All || string.Equals(status.ToString(), filter.ToString(), StringComparison.Ordinal);

    private static bool MatchesSearch(ParameterPlanRowViewModel row, string query)
    {
        if (query.Length == 0) return true;
        return Contains(row.DefinitionGroup, query) || Contains(row.ParameterName, query) ||
               Contains(row.GuidDisplay, query) || Contains(row.CategoriesDisplay, query) || Contains(row.Reason, query);
    }

    private static bool Contains(string value, string query) =>
        value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
}
