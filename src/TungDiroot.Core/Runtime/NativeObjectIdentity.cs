using System.Collections.Generic;

namespace TungDiroot.Core.Runtime;

public static class NativeObjectIdentity
{
    public static bool IsSame<T>(T? expected, T? active) where T : class =>
        expected != null &&
        active != null &&
        EqualityComparer<T>.Default.Equals(expected, active);
}
