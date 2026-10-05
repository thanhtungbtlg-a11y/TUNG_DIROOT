using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using TungDiroot.Core.Layout;

namespace TungDiroot.Addin.Revit;

public sealed class DraftingLegendTableWriter
{
    private const double PointsToFeet = 1D / 864D;
    private readonly RevitStyleResolver _styles;

    public DraftingLegendTableWriter(RevitStyleResolver styles)
    {
        _styles = styles ?? throw new ArgumentNullException(nameof(styles));
    }

    public IReadOnlyList<ElementId> Write(Document document, View view, TableLayoutPlan plan, Guid linkId)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (view == null) throw new ArgumentNullException(nameof(view));
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        var created = new List<ElementId>();

        foreach (var fill in plan.Fills)
        {
            var typeId = _styles.ResolveFilledRegionType(document, fill.Color);
            if (typeId == ElementId.InvalidElementId) continue;
            var loop = RectangleLoop(fill.Bounds);
            var region = FilledRegion.Create(document, typeId, view.Id, new[] { loop });
            ElementLinkTagger.Tag(region, linkId, string.Empty, "Fill");
            created.Add(region.Id);
        }

        foreach (var border in plan.Borders)
        {
            var curve = document.Create.NewDetailCurve(view, Line.CreateBound(Point(border.X1, border.Y1), Point(border.X2, border.Y2)));
            var lineStyle = _styles.ResolveLineStyle(document, border.Style, border.Color, border.MappingKey);
            if (lineStyle != null) curve.LineStyle = lineStyle;
            ElementLinkTagger.Tag(curve, linkId, string.Empty, "Border");
            created.Add(curve.Id);
        }

        foreach (var text in plan.Texts)
        {
            var typeId = _styles.ResolveTextNoteType(document, text.Style, text.MappingKey);
            using var options = new TextNoteOptions(typeId)
            {
                HorizontalAlignment = Horizontal(text.Style.Horizontal),
                VerticalAlignment = Vertical(text.Style.Vertical),
                Rotation = text.Style.RotationDegrees * Math.PI / 180D,
                KeepRotatedTextReadable = true
            };
            var requestedWidth = Math.Max(1D, text.Bounds.Width) * PointsToFeet;
            var width = Math.Max(TextNote.GetMinimumAllowedWidth(document, typeId),
                Math.Min(TextNote.GetMaximumAllowedWidth(document, typeId), requestedWidth));
            var note = TextNote.Create(document, view.Id, TextOrigin(text), width, text.Text, options);
            ElementLinkTagger.Tag(note, linkId, text.CellKey, "Text");
            created.Add(note.Id);
        }

        return created;
    }

    private static XYZ Point(double xPoints, double yPoints) => new XYZ(xPoints * PointsToFeet, -yPoints * PointsToFeet, 0D);

    private static XYZ TextOrigin(TextPlacement text)
    {
        var x = text.Style.Horizontal switch
        {
            TungDiroot.Core.Excel.HorizontalAlignment.Center or TungDiroot.Core.Excel.HorizontalAlignment.CenterContinuous => text.Bounds.X + text.Bounds.Width / 2D,
            TungDiroot.Core.Excel.HorizontalAlignment.Right => text.Bounds.X + text.Bounds.Width,
            _ => text.Bounds.X
        };
        var y = text.Style.Vertical switch
        {
            TungDiroot.Core.Excel.VerticalAlignment.Center => text.Bounds.Y + text.Bounds.Height / 2D,
            TungDiroot.Core.Excel.VerticalAlignment.Bottom => text.Bounds.Y + text.Bounds.Height,
            _ => text.Bounds.Y
        };
        return Point(x, y);
    }

    private static HorizontalTextAlignment Horizontal(TungDiroot.Core.Excel.HorizontalAlignment value) => value switch
    {
        TungDiroot.Core.Excel.HorizontalAlignment.Center or TungDiroot.Core.Excel.HorizontalAlignment.CenterContinuous => HorizontalTextAlignment.Center,
        TungDiroot.Core.Excel.HorizontalAlignment.Right => HorizontalTextAlignment.Right,
        _ => HorizontalTextAlignment.Left
    };

    private static VerticalTextAlignment Vertical(TungDiroot.Core.Excel.VerticalAlignment value) => value switch
    {
        TungDiroot.Core.Excel.VerticalAlignment.Center => VerticalTextAlignment.Middle,
        TungDiroot.Core.Excel.VerticalAlignment.Top => VerticalTextAlignment.Top,
        _ => VerticalTextAlignment.Bottom
    };

    private static CurveLoop RectangleLoop(LayoutRectangle rectangle)
    {
        var topLeft = Point(rectangle.X, rectangle.Y);
        var topRight = Point(rectangle.X + rectangle.Width, rectangle.Y);
        var bottomRight = Point(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height);
        var bottomLeft = Point(rectangle.X, rectangle.Y + rectangle.Height);
        return CurveLoop.Create(new Curve[]
        {
            Line.CreateBound(topLeft, topRight),
            Line.CreateBound(topRight, bottomRight),
            Line.CreateBound(bottomRight, bottomLeft),
            Line.CreateBound(bottomLeft, topLeft)
        });
    }
}
