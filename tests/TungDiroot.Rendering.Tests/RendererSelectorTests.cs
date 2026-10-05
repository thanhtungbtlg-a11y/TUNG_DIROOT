using System;
using Xunit;

namespace TungDiroot.Rendering.Tests;

public sealed class RendererSelectorTests
{
    [Theory]
    [InlineData(RendererPreference.Automatic, true, RendererKind.ExcelHighFidelity)]
    [InlineData(RendererPreference.Automatic, false, RendererKind.BuiltIn)]
    [InlineData(RendererPreference.BuiltInOnly, true, RendererKind.BuiltIn)]
    public void Selects_expected_engine(RendererPreference preference, bool excelAvailable, RendererKind expected)
    {
        Assert.Equal(expected, RendererSelector.Select(preference, new FakeExcelProbe(excelAvailable)));
    }

    [Fact]
    public void Forced_excel_reports_unavailable_instead_of_silent_fallback()
    {
        Assert.Equal("EXCEL_NOT_INSTALLED", Assert.Throws<InvalidOperationException>(() =>
            RendererSelector.Select(RendererPreference.ExcelHighFidelityOnly, new FakeExcelProbe(false))).Message);
    }

    private sealed class FakeExcelProbe : IExcelAvailabilityProbe
    {
        public FakeExcelProbe(bool available) => IsAvailable = available;
        public bool IsAvailable { get; }
    }
}
