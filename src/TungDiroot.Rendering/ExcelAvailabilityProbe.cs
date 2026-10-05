using System;

namespace TungDiroot.Rendering;

public sealed class ExcelAvailabilityProbe : IExcelAvailabilityProbe
{
    public bool IsAvailable => Type.GetTypeFromProgID("Excel.Application", throwOnError: false) != null;
}
