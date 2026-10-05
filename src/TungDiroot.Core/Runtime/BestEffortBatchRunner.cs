using System;
using System.Collections.Generic;

namespace TungDiroot.Core.Runtime;

public static class BestEffortBatchRunner
{
    public static int Run<T>(
        IEnumerable<T> items,
        Action<T> action,
        Func<Exception, bool> isRecoverable,
        Action<T, Exception>? onSkipped = null)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (isRecoverable == null) throw new ArgumentNullException(nameof(isRecoverable));

        var skipped = 0;
        foreach (var item in items)
        {
            try
            {
                action(item);
            }
            catch (Exception exception) when (isRecoverable(exception))
            {
                skipped++;
                onSkipped?.Invoke(item, exception);
            }
        }

        return skipped;
    }
}
