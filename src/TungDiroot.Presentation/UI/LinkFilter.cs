using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TungDiroot.Presentation.Links;

namespace TungDiroot.Presentation.UI;

public enum LinkStatusFilter
{
    All,
    Ready,
    Changed,
    Missing,
    Error
}

public static class LinkFilter
{
    public static void Apply(IEnumerable<LinkRowViewModel> source, string? searchText, LinkStatusFilter status, ObservableCollection<LinkRowViewModel> target)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (target == null) throw new ArgumentNullException(nameof(target));
        var search = searchText?.Trim() ?? string.Empty;
        var filtered = source.Where(item => MatchesStatus(item.Status, status) && MatchesSearch(item, search)).ToArray();
        target.Clear();
        foreach (var item in filtered) target.Add(item);
    }

    private static bool MatchesStatus(LinkStatus value, LinkStatusFilter filter) =>
        filter == LinkStatusFilter.All || string.Equals(value.ToString(), filter.ToString(), StringComparison.Ordinal);

    private static bool MatchesSearch(LinkRowViewModel item, string search) => search.Length == 0 ||
        Contains(item.SourcePath, search) || Contains(item.WorksheetName, search) || Contains(item.RegionReference, search) || Contains(item.TargetViewName, search);

    private static bool Contains(string value, string search) => value?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
}
