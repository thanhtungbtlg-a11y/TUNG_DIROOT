using System;

namespace TungDiroot.Core.Excel;

public sealed record ExcelRangeReference
{
    public ExcelRangeReference(ExcelCellAddress Start, ExcelCellAddress End)
    {
        this.Start = new ExcelCellAddress(Math.Min(Start.Column, End.Column), Math.Min(Start.Row, End.Row));
        this.End = new ExcelCellAddress(Math.Max(Start.Column, End.Column), Math.Max(Start.Row, End.Row));
    }

    public ExcelCellAddress Start { get; init; }

    public ExcelCellAddress End { get; init; }

    public void Deconstruct(out ExcelCellAddress Start, out ExcelCellAddress End)
    {
        Start = this.Start;
        End = this.End;
    }

    public static ExcelRangeReference Parse(string text)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var separatorIndex = text.IndexOf(':');
        if (separatorIndex == -1)
        {
            var cell = ExcelCellAddress.Parse(text);
            return new ExcelRangeReference(cell, cell);
        }

        if (separatorIndex < 1 || separatorIndex != text.LastIndexOf(':') || separatorIndex == text.Length - 1)
        {
            throw new ArgumentException("The value is not a valid Excel range reference.", nameof(text));
        }

        var first = ExcelCellAddress.Parse(text.Substring(0, separatorIndex));
        var second = ExcelCellAddress.Parse(text.Substring(separatorIndex + 1));
        return new ExcelRangeReference(first, second);
    }

    public string ToA1()
    {
        return Start.ToA1() + ":" + End.ToA1();
    }
}
