using System;

namespace TungDiroot.Rendering;

public interface IExcelAvailabilityProbe
{
    bool IsAvailable { get; }
}

public static class RendererSelector
{
    public static RendererKind Select(RendererPreference preference, IExcelAvailabilityProbe probe)
    {
        if (probe == null) throw new ArgumentNullException(nameof(probe));
        if (preference == RendererPreference.BuiltInOnly) return RendererKind.BuiltIn;
        if (preference == RendererPreference.ExcelHighFidelityOnly && !probe.IsAvailable)
            throw new InvalidOperationException("EXCEL_NOT_INSTALLED");
        return probe.IsAvailable ? RendererKind.ExcelHighFidelity : RendererKind.BuiltIn;
    }
}
