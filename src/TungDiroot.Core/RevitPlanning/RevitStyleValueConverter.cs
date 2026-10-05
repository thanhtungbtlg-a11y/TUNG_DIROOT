using System;
using System.Globalization;

namespace TungDiroot.Core.RevitPlanning;

public static class RevitStyleValueConverter
{
    public static double ScheduleTextSize(double points) => Math.Max(1D, points);
    public static int ScheduleTextOrientation(double degrees)
    {
        var value = (int)Math.Round((degrees % 360D) * 10D) % 3600;
        return value < 0 ? value + 3600 : value;
    }
    public static int TextColorParameter(byte red, byte green, byte blue) => red | (green << 8) | (blue << 16);
    public static string TextColorArgb(int color) => "FF" +
        (color & 0xFF).ToString("X2", CultureInfo.InvariantCulture) +
        ((color >> 8) & 0xFF).ToString("X2", CultureInfo.InvariantCulture) +
        ((color >> 16) & 0xFF).ToString("X2", CultureInfo.InvariantCulture);
}
