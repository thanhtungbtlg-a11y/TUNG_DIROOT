using TungDiroot.Core.Documents;
using Xunit;

namespace TungDiroot.Core.Tests.Documents;

public sealed class DocumentPagePlacementPlannerTests
{
    [Fact]
    public void Plan_arranges_pages_in_requested_columns_using_tallest_row_item()
    {
        var pages = new[]
        {
            new DocumentPageSize(1, 10, 20),
            new DocumentPageSize(2, 15, 12),
            new DocumentPageSize(3, 8, 5)
        };

        var result = DocumentPagePlacementPlanner.Plan(pages, 2, 2);

        Assert.Equal(3, result.Count);
        Assert.Equal((0d, 0d), (result[0].TopLeftX, result[0].TopLeftY));
        Assert.Equal((12d, 0d), (result[1].TopLeftX, result[1].TopLeftY));
        Assert.Equal((0d, -22d), (result[2].TopLeftX, result[2].TopLeftY));
    }
}
