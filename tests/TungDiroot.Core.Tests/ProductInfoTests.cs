using Xunit;

namespace TungDiroot.Core.Tests;

public sealed class ProductInfoTests
{
    [Fact]
    public void Product_identity_is_stable()
    {
        Assert.Equal("TUNG_DIROOT", ProductInfo.Name);
        Assert.Equal(1, ProductInfo.SchemaVersion);
    }
}
