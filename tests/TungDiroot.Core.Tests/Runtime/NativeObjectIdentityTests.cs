using System;
using TungDiroot.Core.Runtime;
using Xunit;

namespace TungDiroot.Core.Tests.Runtime;

public sealed class NativeObjectIdentityTests
{
    [Fact]
    public void Same_native_object_accepts_distinct_managed_wrappers_with_equal_native_identity()
    {
        var capturedWrapper = new NativeWrapper(42);
        var activeWrapper = new NativeWrapper(42);

        Assert.NotSame(capturedWrapper, activeWrapper);
        Assert.True(NativeObjectIdentity.IsSame(capturedWrapper, activeWrapper));
    }

    [Fact]
    public void Different_native_objects_are_rejected()
    {
        Assert.False(NativeObjectIdentity.IsSame(new NativeWrapper(42), new NativeWrapper(99)));
        Assert.False(NativeObjectIdentity.IsSame<NativeWrapper>(new NativeWrapper(42), null));
    }

    private sealed class NativeWrapper : IEquatable<NativeWrapper>
    {
        private readonly int _nativeId;

        public NativeWrapper(int nativeId) => _nativeId = nativeId;

        public bool Equals(NativeWrapper? other) => other != null && other._nativeId == _nativeId;
        public override bool Equals(object? obj) => Equals(obj as NativeWrapper);
        public override int GetHashCode() => _nativeId;
    }
}
