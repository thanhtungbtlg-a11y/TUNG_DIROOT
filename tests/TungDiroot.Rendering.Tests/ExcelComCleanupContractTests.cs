using System;
using System.IO;
using Xunit;

namespace TungDiroot.Rendering.Tests;

public sealed class ExcelComCleanupContractTests
{
    [Fact]
    public void Bridge_is_disposed_even_when_export_fails()
    {
        var factory = new FakeFactory();
        using var destination = new MemoryStream();

        Assert.Throws<IOException>(() => new ExcelComWorkbookImageRenderer(factory)
            .RenderFileRange("A.xlsx", "PCCC", "A1:B2", destination));

        Assert.True(factory.Bridge.Disposed);
    }

    private sealed class FakeFactory : IExcelComBridgeFactory
    {
        public FakeBridge Bridge { get; } = new FakeBridge();
        public IExcelComBridge Create() => Bridge;
    }

    private sealed class FakeBridge : IExcelComBridge
    {
        public bool Disposed { get; private set; }
        public void ExportRangePng(string path, string worksheetName, string rangeReference, string outputPath) => throw new IOException("test");
        public void Dispose() => Disposed = true;
    }
}
