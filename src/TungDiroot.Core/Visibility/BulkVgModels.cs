using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TungDiroot.Core.Visibility;

public enum BulkVgTargetKind
{
    View,
    ViewTemplate,
    Sheet
}

public enum BulkVgApplyMode
{
    Merge,
    Replace,
    UpdateExisting
}

public sealed record BulkVgFilterPlan(
    IReadOnlyList<long> Remove,
    IReadOnlyList<long> Add,
    IReadOnlyList<long> Update,
    IReadOnlyList<long> Skip,
    IReadOnlyList<long> FinalOrder,
    bool RebuildOrder);

public static class BulkVgFilterPlanner
{
    public static BulkVgFilterPlan Plan(
        IEnumerable<long> targetOrderedFilters,
        IEnumerable<long> selectedSourceOrderedFilters,
        BulkVgApplyMode mode)
    {
        if (targetOrderedFilters == null) throw new ArgumentNullException(nameof(targetOrderedFilters));
        if (selectedSourceOrderedFilters == null) throw new ArgumentNullException(nameof(selectedSourceOrderedFilters));
        if (!Enum.IsDefined(typeof(BulkVgApplyMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var target = targetOrderedFilters.Distinct().ToArray();
        var selected = selectedSourceOrderedFilters.Distinct().ToArray();
        var targetSet = new HashSet<long>(target);
        var selectedSet = new HashSet<long>(selected);
        var update = selected.Where(targetSet.Contains).ToArray();
        var missing = selected.Where(id => !targetSet.Contains(id)).ToArray();
        var remove = mode == BulkVgApplyMode.Replace && selected.Length > 0
            ? target.Where(id => !selectedSet.Contains(id)).ToArray() : Array.Empty<long>();
        var add = mode == BulkVgApplyMode.UpdateExisting ? Array.Empty<long>() : missing;
        var skip = mode == BulkVgApplyMode.UpdateExisting ? missing : Array.Empty<long>();
        var afterRemoveAndAdd = target.Except(remove).Concat(add).ToArray();
        var finalOrder = mode == BulkVgApplyMode.Replace && selected.Length > 0 ? selected : afterRemoveAndAdd;
        return new BulkVgFilterPlan(
            Array.AsReadOnly(remove), Array.AsReadOnly(add), Array.AsReadOnly(update), Array.AsReadOnly(skip),
            Array.AsReadOnly(finalOrder), !afterRemoveAndAdd.SequenceEqual(finalOrder));
    }
}

public static class BulkVgOverrideMerger
{
    public static TGraphics Merge<TGraphics>(
        TGraphics source,
        TGraphics target,
        BulkVgApplyOptions options,
        Func<TGraphics, TGraphics> clone,
        Action<TGraphics, TGraphics> copyProjection,
        Action<TGraphics, TGraphics> copyCut,
        Action<TGraphics, TGraphics> copyFill,
        Action<TGraphics, TGraphics> copyTransparencyAndHalftone)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        var result = clone(target);
        if (options.CopyProjection) copyProjection(result, source);
        if (options.CopyCut) copyCut(result, source);
        if (options.CopyFill) copyFill(result, source);
        if (options.CopyTransparencyAndHalftone) copyTransparencyAndHalftone(result, source);
        return result;
    }
}

public sealed record BulkVgTargetSelection(long Id, BulkVgTargetKind Kind, IReadOnlyList<long> ViewportViewIds);

public sealed record BulkVgApplyOptions(
    bool CopyVisibility,
    bool CopyProjection,
    bool CopyCut,
    bool CopyFill,
    bool CopyTransparencyAndHalftone)
{
    public bool HasAny => CopyVisibility || CopyProjection || CopyCut || CopyFill || CopyTransparencyAndHalftone;
}

public sealed record BulkVgComparableState(
    bool Visible,
    string ProjectionSignature,
    string CutSignature,
    string FillSignature,
    int Transparency,
    bool Halftone,
    bool Enabled = true);

public static class BulkVgSelectionPlanner
{
    public static IReadOnlyList<long> Expand(IEnumerable<BulkVgTargetSelection> selected, long sourceViewId)
    {
        if (selected == null) throw new ArgumentNullException(nameof(selected));
        var result = new List<long>();
        var seen = new HashSet<long>();
        foreach (var item in selected)
        {
            IEnumerable<long> ids = item.Kind switch
            {
                BulkVgTargetKind.View or BulkVgTargetKind.ViewTemplate => new[] { item.Id },
                BulkVgTargetKind.Sheet => item.ViewportViewIds,
                _ => throw new ArgumentOutOfRangeException(nameof(selected), "TARGET_KIND_INVALID")
            };
            foreach (var id in ids)
                if (id != sourceViewId && seen.Add(id)) result.Add(id);
        }
        return new ReadOnlyCollection<long>(result);
    }
}

public static class BulkVgDifferenceDetector
{
    public static IReadOnlyList<string> Compare(
        BulkVgComparableState source,
        BulkVgComparableState target,
        BulkVgApplyOptions options)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (options == null) throw new ArgumentNullException(nameof(options));
        var result = new List<string>();
        if (options.CopyVisibility && (source.Visible != target.Visible || source.Enabled != target.Enabled)) result.Add("Visibility");
        if (options.CopyProjection && !string.Equals(source.ProjectionSignature, target.ProjectionSignature, StringComparison.Ordinal)) result.Add("Projection");
        if (options.CopyCut && !string.Equals(source.CutSignature, target.CutSignature, StringComparison.Ordinal)) result.Add("Cut");
        if (options.CopyFill && !string.Equals(source.FillSignature, target.FillSignature, StringComparison.Ordinal)) result.Add("Fill");
        if (options.CopyTransparencyAndHalftone && (source.Transparency != target.Transparency || source.Halftone != target.Halftone))
            result.Add("Transparency/Halftone");
        return new ReadOnlyCollection<string>(result);
    }
}
