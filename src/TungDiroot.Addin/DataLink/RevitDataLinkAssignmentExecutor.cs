using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using TungDiroot.Core.DataLink;

namespace TungDiroot.Addin.DataLink;

public sealed record DataLinkApplyResult(
    string ElementUniqueId,
    string ParameterStableId,
    bool Success,
    DataLinkChangeStatus Status,
    string Message);

public sealed class RevitDataLinkAssignmentExecutor
{
    public IReadOnlyList<DataLinkApplyResult> Execute(Document document, DataLinkChangePlan plan)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (document.IsLinked) throw new InvalidOperationException("DataLink không cập nhật linked document.");
        var ready = plan.Changes.Where(item => item.Status == DataLinkChangeStatus.Ready).ToArray();
        if (ready.Length == 0) return Array.Empty<DataLinkApplyResult>();
        if (plan.DocumentStamp != RevitDataLinkDocumentStamp.Create(document))
            return ready.Select(item => Result(item, false, DataLinkChangeStatus.DifferentDocument, "Model hiện tại không khớp file Excel.")).ToArray();

        var results = new List<DataLinkApplyResult>(ready.Length);
        using var group = new TransactionGroup(document, "TUNG_DIROOT - DataLink");
        if (group.Start() != TransactionStatus.Started)
            return ready.Select(item => Result(item, false, DataLinkChangeStatus.NotEditable, "Không thể bắt đầu TransactionGroup.")).ToArray();

        var committed = false;
        foreach (var targetGroup in ready.GroupBy(TargetKey))
        {
            using var transaction = new Transaction(document, "TUNG_DIROOT - DataLink item");
            try
            {
                var targets = Prepare(document, targetGroup.ToArray());
                if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("Không thể bắt đầu transaction.");
                foreach (var target in targets)
                {
                    if (!RevitParameterValueCodec.TrySet(target.Parameter, target.Change.ParsedInternal ?? string.Empty, out var message))
                        throw new InvalidOperationException(message);
                }
                if (transaction.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Transaction không commit được.");
                committed = true;
                results.AddRange(targetGroup.Select(item => Result(item, true, DataLinkChangeStatus.Ready, "Đã cập nhật.")));
            }
            catch (Exception exception)
            {
                if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                results.AddRange(targetGroup.Select(item => Result(item, false, FailureStatus(exception), exception.Message)));
            }
        }

        if (committed) group.Assimilate();
        else group.RollBack();
        return results;
    }

    private static IReadOnlyList<PreparedChange> Prepare(Document document, DataLinkCandidateChange[] changes)
    {
        var prepared = new List<PreparedChange>();
        foreach (var change in changes.GroupBy(item => item.Row.Parameter.StableId).Select(group => group.First()))
        {
            var uniqueId = change.Row.Parameter.IsType ? change.Row.TypeUniqueId : change.Row.ElementUniqueId;
            var target = string.IsNullOrWhiteSpace(uniqueId) ? null : document.GetElement(uniqueId);
            if (target == null) throw new DataLinkApplyException(DataLinkChangeStatus.ElementMissing, "Element hoặc type không còn tồn tại.");
            if (!RevitDataLinkSnapshotReader.IsEditable(document, target))
                throw new DataLinkApplyException(DataLinkChangeStatus.NotEditable, "Element hoặc type đang thuộc quyền chỉnh sửa của người khác.");
            var parameter = RevitParameterValueCodec.Resolve(target, change.Row.Parameter);
            if (parameter == null) throw new DataLinkApplyException(DataLinkChangeStatus.ParameterMissing, "Parameter không còn tồn tại.");
            if (parameter.IsReadOnly) throw new DataLinkApplyException(DataLinkChangeStatus.ReadOnly, "Parameter chỉ đọc.");
            if (!string.Equals(RevitParameterValueCodec.InternalValue(parameter), change.Row.OriginalInternal, StringComparison.Ordinal))
                throw new DataLinkApplyException(DataLinkChangeStatus.StaleModelValue, "Giá trị Revit đã thay đổi sau khi xem trước.");
            prepared.Add(new PreparedChange(change, parameter));
        }
        return prepared;
    }

    private static string TargetKey(DataLinkCandidateChange change) => change.Row.Parameter.IsType
        ? "T:" + change.Row.TypeUniqueId
        : "I:" + change.Row.ElementUniqueId;

    private static DataLinkApplyResult Result(DataLinkCandidateChange change, bool success, DataLinkChangeStatus status, string message) =>
        new(change.Row.ElementUniqueId, change.Row.Parameter.StableId, success, status, message);

    private static DataLinkChangeStatus FailureStatus(Exception exception) =>
        exception is DataLinkApplyException dataLink ? dataLink.Status : DataLinkChangeStatus.InvalidValue;

    private sealed record PreparedChange(DataLinkCandidateChange Change, Parameter Parameter);

    private sealed class DataLinkApplyException : Exception
    {
        public DataLinkApplyException(DataLinkChangeStatus status, string message) : base(message) => Status = status;
        public DataLinkChangeStatus Status { get; }
    }
}
