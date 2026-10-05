using System;

namespace TungDiroot.Core.Parameters;

public static class TemporaryValueScope
{
    public static T Use<T>(
        Func<string?> getCurrent,
        Action<string?> setCurrent,
        string temporaryValue,
        Func<T> action)
    {
        if (getCurrent == null) throw new ArgumentNullException(nameof(getCurrent));
        if (setCurrent == null) throw new ArgumentNullException(nameof(setCurrent));
        if (temporaryValue == null) throw new ArgumentNullException(nameof(temporaryValue));
        if (action == null) throw new ArgumentNullException(nameof(action));

        var original = getCurrent();
        setCurrent(temporaryValue);
        try
        {
            return action();
        }
        finally
        {
            setCurrent(original);
        }
    }
}
