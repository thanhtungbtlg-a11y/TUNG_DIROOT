using System;
using System.Collections.Generic;

namespace TungDiroot.Core.Excel;

public enum WorkbookDrawingKind
{
    Image,
    Chart,
    Shape,
    SmartArt,
    Other
}

public sealed class WorkbookDrawingItem
{
    private readonly byte[] _imageBytes;

    public WorkbookDrawingItem(WorkbookDrawingKind kind, string anchor, string? relationshipId, byte[]? imageBytes, WorkbookDrawingPreview? cachedPreview)
    {
        Kind = kind;
        Anchor = anchor ?? throw new ArgumentNullException(nameof(anchor));
        RelationshipId = relationshipId;
        _imageBytes = imageBytes == null ? Array.Empty<byte>() : (byte[])imageBytes.Clone();
        CachedPreview = cachedPreview;
    }

    public WorkbookDrawingKind Kind { get; }

    public string Anchor { get; }

    public string? RelationshipId { get; }

    public ReadOnlyMemory<byte> ImageBytes => new ReadOnlyMemory<byte>((byte[])_imageBytes.Clone());

    public WorkbookDrawingPreview? CachedPreview { get; }
}

public sealed class WorkbookDrawingPreview
{
    private readonly byte[] _bytes;

    public WorkbookDrawingPreview(string relationshipId, byte[] bytes)
    {
        RelationshipId = relationshipId ?? throw new ArgumentNullException(nameof(relationshipId));
        _bytes = bytes == null ? throw new ArgumentNullException(nameof(bytes)) : (byte[])bytes.Clone();
    }

    public string RelationshipId { get; }

    public ReadOnlyMemory<byte> Bytes => new ReadOnlyMemory<byte>((byte[])_bytes.Clone());
}

public sealed class WorkbookDrawingModel
{
    public WorkbookDrawingModel(IEnumerable<WorkbookDrawingItem> items)
    {
        Items = new ReadOnlyList<WorkbookDrawingItem>(items);
    }

    public IReadOnlyList<WorkbookDrawingItem> Items { get; }
}
