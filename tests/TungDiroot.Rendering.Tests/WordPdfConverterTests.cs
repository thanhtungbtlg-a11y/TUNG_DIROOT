using System;
using System.IO;
using Xunit;

namespace TungDiroot.Rendering.Tests;

public sealed class WordPdfConverterTests
{
    [Fact]
    public void Bridge_is_disposed_when_conversion_fails()
    {
        var factory = new FakeFactory();

        Assert.Throws<IOException>(() => new WordPdfConverter(factory).Convert("A.docx", "A.pdf"));

        Assert.True(factory.Bridge.Disposed);
    }

    private sealed class FakeFactory : IWordPdfBridgeFactory
    {
        public FakeBridge Bridge { get; } = new FakeBridge();
        public IWordPdfBridge Create() => Bridge;
    }

    private sealed class FakeBridge : IWordPdfBridge
    {
        public bool Disposed { get; private set; }
        public void Convert(string sourcePath, string destinationPdfPath) => throw new IOException("test");
        public void Dispose() => Disposed = true;
    }
}
