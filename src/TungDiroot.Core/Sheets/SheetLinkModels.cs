using System;
using System.Collections.Generic;

namespace TungDiroot.Core.SheetTools;

public enum SheetLinkAction
{
    Create,
    Update,
    Skip
}

public enum SheetLinkViewKind
{
    Drafting,
    FloorPlan,
    CeilingPlan
}

public sealed record SheetLinkSheetRow(
    bool Apply,
    SheetLinkAction Action,
    string? UniqueId,
    string SheetNumber,
    string SheetName,
    string TitleBlockType,
    string RevisionSequences);

public sealed record SheetLinkViewRow(
    bool Apply,
    SheetLinkAction Action,
    string? UniqueId,
    string ViewName,
    SheetLinkViewKind ViewKind,
    string LevelName,
    string ViewTemplate,
    string SheetNumber,
    double? CenterXMillimeters,
    double? CenterYMillimeters);

public sealed record SheetLinkWorkbook(
    IReadOnlyList<SheetLinkSheetRow> Sheets,
    IReadOnlyList<SheetLinkViewRow> Views)
{
    public IReadOnlyList<SheetLinkRevisionRow> Revisions { get; init; } = Array.Empty<SheetLinkRevisionRow>();
    public IReadOnlyList<int>? SheetSourceRows { get; init; }
    public IReadOnlyList<int>? ViewSourceRows { get; init; }
    public IReadOnlyList<int>? RevisionSourceRows { get; init; }

    public SheetLinkWorkbook(IReadOnlyList<SheetLinkSheetRow> sheets, IReadOnlyList<SheetLinkViewRow> views,
        IReadOnlyList<SheetLinkRevisionRow> revisions) : this(sheets, views) => Revisions = revisions;

    public int SourceRow(SheetLinkRowKind kind, int index)
    {
        var sourceRows = kind switch
        {
            SheetLinkRowKind.Sheet => SheetSourceRows,
            SheetLinkRowKind.View => ViewSourceRows,
            SheetLinkRowKind.Revision => RevisionSourceRows,
            _ => null
        };
        return sourceRows != null && index >= 0 && index < sourceRows.Count ? sourceRows[index] : index + 2;
    }
}

public enum SheetLinkRevisionVisibility { CloudAndTag, TagOnly, Hidden }

public sealed record SheetLinkRevisionRow(
    bool Apply,
    SheetLinkAction Action,
    string? UniqueId,
    string RevisionKey,
    int? SequenceNumber,
    string Description,
    string RevisionDate,
    bool Issued,
    string IssuedBy,
    string IssuedTo,
    SheetLinkRevisionVisibility Visibility);

public sealed record SheetLinkRevisionReference(int? SequenceNumber, string? RevisionKey);
