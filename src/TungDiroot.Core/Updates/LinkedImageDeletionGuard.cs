using System;
using System.Collections.Generic;
using System.Linq;

namespace TungDiroot.Core.Updates;

public sealed record ImageInstanceReference(long InstanceId, long TypeId);

public static class LinkedImageDeletionGuard
{
    public static void Validate(IEnumerable<long> linkedTypeIds, IEnumerable<long> linkedInstanceIds,
        IEnumerable<ImageInstanceReference> allImageInstances)
    {
        var types = new HashSet<long>(linkedTypeIds ?? throw new ArgumentNullException(nameof(linkedTypeIds)));
        var instances = new HashSet<long>(linkedInstanceIds ?? throw new ArgumentNullException(nameof(linkedInstanceIds)));
        if (allImageInstances == null) throw new ArgumentNullException(nameof(allImageInstances));
        if (allImageInstances.Any(image => types.Contains(image.TypeId) && !instances.Contains(image.InstanceId)))
            throw new InvalidOperationException("LINKED_IMAGE_TYPE_SHARED: Ảnh Excel đang được dùng ngoài liên kết này. " +
                "Dừng dựng lại để giữ nguyên các ảnh khác; hãy tạo Image Type riêng cho các bản sao trước khi thử lại.");
    }
}
