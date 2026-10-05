using System;
using TungDiroot.Core.Updates;
using Xunit;

namespace TungDiroot.Core.Tests.Updates;

public sealed class LinkedImageDeletionGuardTests
{
    [Fact]
    public void Reused_linked_type_blocks_deletion_of_untagged_image_in_another_view()
    {
        var instances = new[] { new ImageInstanceReference(101, 10), new ImageInstanceReference(102, 10) };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            LinkedImageDeletionGuard.Validate(new long[] { 10 }, new long[] { 101 }, instances));

        Assert.StartsWith("LINKED_IMAGE_TYPE_SHARED:", exception.Message);
    }

    [Fact]
    public void Image_owned_by_another_link_also_blocks_shared_type_deletion() =>
        Assert.Throws<InvalidOperationException>(() => LinkedImageDeletionGuard.Validate(
            new long[] { 10, 20 }, new long[] { 101, 201 },
            new[] { new ImageInstanceReference(101, 10), new ImageInstanceReference(201, 20), new ImageInstanceReference(202, 20) }));

    [Fact]
    public void All_instances_owned_by_this_link_allow_replacement() =>
        LinkedImageDeletionGuard.Validate(new long[] { 10 }, new long[] { 101, 102 },
            new[] { new ImageInstanceReference(101, 10), new ImageInstanceReference(102, 10) });

    [Fact]
    public void Unrelated_image_types_do_not_block_replacement() =>
        LinkedImageDeletionGuard.Validate(new long[] { 10 }, new long[] { 101 },
            new[] { new ImageInstanceReference(101, 10), new ImageInstanceReference(999, 90) });

    [Fact]
    public void Empty_managed_image_scope_allows_table_rebuild() =>
        LinkedImageDeletionGuard.Validate(Array.Empty<long>(), Array.Empty<long>(),
            new[] { new ImageInstanceReference(999, 90) });
}
