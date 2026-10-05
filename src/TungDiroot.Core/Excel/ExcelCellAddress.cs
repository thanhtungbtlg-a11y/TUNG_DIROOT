using System;
using System.Globalization;

namespace TungDiroot.Core.Excel;

public readonly record struct ExcelCellAddress
{
    public const int MaximumColumn = 16384;
    public const int MaximumRow = 1048576;

    private readonly int _column;
    private readonly int _row;

    public ExcelCellAddress(int Column, int Row)
    {
        ValidateColumn(Column, nameof(Column));
        ValidateRow(Row, nameof(Row));
        _column = Column;
        _row = Row;
    }

    public int Column
    {
        get => _column;
        init
        {
            ValidateColumn(value, nameof(Column));
            _column = value;
        }
    }

    public int Row
    {
        get => _row;
        init
        {
            ValidateRow(value, nameof(Row));
            _row = value;
        }
    }

    public void Deconstruct(out int Column, out int Row)
    {
        Column = this.Column;
        Row = this.Row;
    }

    public static ExcelCellAddress Parse(string text)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var index = 0;
        if (index < text.Length && text[index] == '$')
        {
            index++;
        }

        var firstColumnCharacter = index;
        var column = 0;
        try
        {
            while (index < text.Length && IsAsciiLetter(text[index]))
            {
                column = checked((column * 26) + (char.ToUpperInvariant(text[index]) - 'A' + 1));
                index++;
            }
        }
        catch (OverflowException)
        {
            throw InvalidAddress(text);
        }

        if (index == firstColumnCharacter)
        {
            throw InvalidAddress(text);
        }

        if (index < text.Length && text[index] == '$')
        {
            index++;
        }

        var firstRowCharacter = index;
        var row = 0;
        try
        {
            while (index < text.Length && text[index] >= '0' && text[index] <= '9')
            {
                row = checked((row * 10) + (text[index] - '0'));
                index++;
            }
        }
        catch (OverflowException)
        {
            throw InvalidAddress(text);
        }

        if (index != text.Length || index == firstRowCharacter)
        {
            throw InvalidAddress(text);
        }

        try
        {
            return new ExcelCellAddress(column, row);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw InvalidAddress(text);
        }
    }

    public string ToA1()
    {
        if (!IsInsideExcelGrid(Column, Row))
        {
            throw new InvalidOperationException("The cell address is outside the Excel worksheet grid.");
        }

        var remainingColumn = Column;
        var columnName = string.Empty;
        while (remainingColumn > 0)
        {
            remainingColumn--;
            columnName = (char)('A' + (remainingColumn % 26)) + columnName;
            remainingColumn /= 26;
        }

        return columnName + Row.ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsAsciiLetter(char character)
    {
        return (character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z');
    }

    internal static bool IsInsideExcelGrid(int column, int row)
    {
        return column >= 1 && column <= MaximumColumn && row >= 1 && row <= MaximumRow;
    }

    private static void ValidateColumn(int column, string parameterName)
    {
        if (column < 1 || column > MaximumColumn)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Excel columns must be between 1 and 16384.");
        }
    }

    private static void ValidateRow(int row, string parameterName)
    {
        if (row < 1 || row > MaximumRow)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Excel rows must be between 1 and 1048576.");
        }
    }

    private static ArgumentException InvalidAddress(string text)
    {
        return new ArgumentException("The value is not a valid Excel cell address.", nameof(text));
    }
}
