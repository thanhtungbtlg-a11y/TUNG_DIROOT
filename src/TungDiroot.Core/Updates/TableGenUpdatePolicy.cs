using System;
using TungDiroot.Core.Import;
using TungDiroot.Core.Links;

namespace TungDiroot.Core.Updates;

public static class TableGenUpdatePolicy
{
    public static void RequireTransactionStarted(bool started)
    {
        if (!started) throw new InvalidOperationException("REVIT_TRANSACTION_NOT_STARTED");
    }

    public static void RequireTransactionCommitted(bool committed)
    {
        if (!committed) throw new InvalidOperationException("REVIT_TRANSACTION_NOT_COMMITTED");
    }

    public static bool ShouldSkip(SourceChange change, bool forceRecreate = false) =>
        !forceRecreate && change == SourceChange.Unchanged;

    public static UpdatePlan CreatePlan(UpdatePlanInput input, bool forceRecreate = false)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (forceRecreate)
            return new UpdatePlan(UpdateAction.Recreate, input.After, Array.Empty<CellValueChange>(), "EXPLICIT_RECREATE");
        if (input.SourceChanged && input.ImportMode == ImportMode.RevitTable &&
            input.RequestedBehavior == UpdateBehavior.DataOnly && !input.StructureCompatible)
            throw new InvalidOperationException(
                "DATA_ONLY_STRUCTURE_CHANGED: Cấu trúc bảng đã thay đổi. DataOnly không dựng lại bảng. " +
                "Hãy chọn Dựng lại bảng (Recreate) nếu đồng ý mất chỉnh sửa thủ công. / " +
                "Table structure changed. Choose Recreate explicitly; manual edits may be lost.");
        return UpdatePlanner.Create(input);
    }
}
