using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace TungDiroot.Core.Documents;

public sealed class DocumentPageSelection : IReadOnlyList<int>
{
    public const int MaximumPageCount = 500;
    private readonly ReadOnlyCollection<int> _pages;

    private DocumentPageSelection(IEnumerable<int> pages) =>
        _pages = new ReadOnlyCollection<int>(pages.ToArray());

    public int Count => _pages.Count;
    public int this[int index] => _pages[index];

    public static DocumentPageSelection Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("PAGE_SELECTION_REQUIRED", nameof(expression));

        var pages = new SortedSet<int>();
        foreach (var rawToken in expression.Split(','))
        {
            var token = rawToken.Trim();
            if (token.Length == 0)
                throw new ArgumentException("PAGE_SELECTION_INVALID", nameof(expression));

            var range = token.Split('-');
            if (range.Length == 1)
            {
                AddPage(pages, ParsePage(range[0], expression));
                continue;
            }
            if (range.Length != 2 || range[0].Trim().Length == 0 || range[1].Trim().Length == 0)
                throw new ArgumentException("PAGE_SELECTION_INVALID", nameof(expression));

            var first = ParsePage(range[0], expression);
            var last = ParsePage(range[1], expression);
            var start = Math.Min(first, last);
            var end = Math.Max(first, last);
            if (end - start + 1 > MaximumPageCount)
                throw new ArgumentException("PAGE_SELECTION_TOO_LARGE", nameof(expression));
            for (var page = start; page <= end; page++) AddPage(pages, page);
        }

        if (pages.Count == 0) throw new ArgumentException("PAGE_SELECTION_REQUIRED", nameof(expression));
        return new DocumentPageSelection(pages);
    }

    public override string ToString()
    {
        var tokens = new List<string>();
        var index = 0;
        while (index < _pages.Count)
        {
            var start = _pages[index];
            var end = start;
            while (index + 1 < _pages.Count && _pages[index + 1] == end + 1)
            {
                index++;
                end = _pages[index];
            }
            tokens.Add(start == end
                ? start.ToString(CultureInfo.InvariantCulture)
                : start.ToString(CultureInfo.InvariantCulture) + "-" + end.ToString(CultureInfo.InvariantCulture));
            index++;
        }
        return string.Join(",", tokens);
    }

    public IEnumerator<int> GetEnumerator() => _pages.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static int ParsePage(string value, string expression)
    {
        if (!int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var page) || page <= 0)
            throw new ArgumentException("PAGE_SELECTION_INVALID", nameof(expression));
        return page;
    }

    private static void AddPage(ISet<int> pages, int page)
    {
        pages.Add(page);
        if (pages.Count > MaximumPageCount) throw new ArgumentException("PAGE_SELECTION_TOO_LARGE", "expression");
    }
}
