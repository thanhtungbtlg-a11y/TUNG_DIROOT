using TungDiroot.Core.Import;
using Xunit;

namespace TungDiroot.Core.Tests.Import;

public sealed class ImportSizeEstimatorTests
{
    [Fact]
    public void Revit_table_over_5000_cells_requires_confirmation()
    {
        var estimate = ImportSizeEstimator.Estimate(101, 50, ImportMode.RevitTable);

        Assert.True(estimate.RequiresConfirmation);
        Assert.Equal(5050, estimate.CellCount);
        Assert.True(estimate.EstimatedElementCount > estimate.CellCount);
    }

    [Fact]
    public void Image_import_does_not_use_element_count_confirmation()
    {
        Assert.False(ImportSizeEstimator.Estimate(1000, 1000, ImportMode.Image).RequiresConfirmation);
    }
}
