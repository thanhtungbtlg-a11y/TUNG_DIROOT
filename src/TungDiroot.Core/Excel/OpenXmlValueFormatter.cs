using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TungDiroot.Core.Excel;

internal sealed class OpenXmlValueFormatter
{
    private static readonly Regex DateTokens = new Regex("[dmyhs]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly bool _date1904;

    public OpenXmlValueFormatter(bool date1904)
    {
        _date1904 = date1904;
    }

    public string FormatNumber(string rawValue, string numberFormatCode)
    {
        if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return rawValue;
        }

        if (ContainsPercent(numberFormatCode))
        {
            var precision = DecimalPlaces(numberFormatCode);
            return (value * 100D).ToString("F" + precision, CultureInfo.InvariantCulture) + "%";
        }

        if (LooksLikeDate(numberFormatCode))
        {
            try
            {
                var date = DateTime.FromOADate(value);
                if (_date1904)
                {
                    date = date.AddDays(1462D);
                }

                return date.ToString(ToDotNetDateFormat(numberFormatCode), CultureInfo.InvariantCulture);
            }
            catch (ArgumentException)
            {
                return rawValue;
            }
        }

        if (string.Equals(numberFormatCode, "General", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToString("G15", CultureInfo.InvariantCulture);
        }

        try
        {
            return value.ToString(ToDotNetNumberFormat(numberFormatCode), CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return value.ToString("G15", CultureInfo.InvariantCulture);
        }
    }

    private static bool ContainsPercent(string format)
    {
        return format.IndexOf('%') >= 0;
    }

    private static int DecimalPlaces(string format)
    {
        var decimalIndex = format.IndexOf('.');
        if (decimalIndex < 0)
        {
            return 0;
        }

        var count = 0;
        for (var index = decimalIndex + 1; index < format.Length && (format[index] == '0' || format[index] == '#'); index++)
        {
            count++;
        }

        return count;
    }

    private static bool LooksLikeDate(string format)
    {
        return DateTokens.IsMatch(RemoveQuotedText(format));
    }

    private static string ToDotNetDateFormat(string format)
    {
        var result = new System.Text.StringBuilder(format.Length);
        for (var index = 0; index < format.Length;)
        {
            if (format[index] == '"')
            {
                var endQuote = format.IndexOf('"', index + 1);
                if (endQuote < 0)
                {
                    result.Append(format[index]);
                    index++;
                }
                else
                {
                    result.Append(format, index, endQuote - index + 1);
                    index = endQuote + 1;
                }

                continue;
            }

            if (MatchesAmPm(format, index))
            {
                result.Append("tt");
                index += 5;
                continue;
            }

            var token = char.ToLowerInvariant(format[index]);
            if (token == 'd' || token == 'm' || token == 'y' || token == 'h' || token == 's')
            {
                var tokenEnd = index + 1;
                while (tokenEnd < format.Length && char.ToLowerInvariant(format[tokenEnd]) == token)
                {
                    tokenEnd++;
                }

                var dotNetToken = token == 'm' && !IsMinuteToken(format, index, tokenEnd) ? 'M' : token;
                result.Append(dotNetToken, tokenEnd - index);
                index = tokenEnd;
                continue;
            }

            result.Append(format[index]);
            index++;
        }

        return result.ToString();
    }

    private static bool MatchesAmPm(string format, int index)
    {
        return index + 5 <= format.Length && string.Compare(format, index, "AM/PM", 0, 5, StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static bool IsMinuteToken(string format, int start, int end)
    {
        if (start > 0 && format[start - 1] == '[' && end < format.Length && format[end] == ']')
        {
            return true;
        }

        var previousToken = FindAdjacentDateToken(format, start - 1, -1);
        var nextToken = FindAdjacentDateToken(format, end, 1);
        return previousToken == 'h' || nextToken == 's';
    }

    private static char FindAdjacentDateToken(string format, int index, int direction)
    {
        while (index >= 0 && index < format.Length)
        {
            var token = char.ToLowerInvariant(format[index]);
            if (token == 'd' || token == 'm' || token == 'y' || token == 'h' || token == 's')
            {
                return token;
            }

            if (format[index] == '"')
            {
                return '\0';
            }

            index += direction;
        }

        return '\0';
    }

    private static string ToDotNetNumberFormat(string format)
    {
        var sectionEnd = format.IndexOf(';');
        return (sectionEnd < 0 ? format : format.Substring(0, sectionEnd)).Replace("_", string.Empty).Replace("*", string.Empty);
    }

    private static string RemoveQuotedText(string format)
    {
        return Regex.Replace(format, "\\\"[^\\\"]*\\\"", string.Empty);
    }
}
