using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using TungDiroot.Core.DataLink;

namespace TungDiroot.Addin.DataLink;

public sealed class RevitDataLinkCurrentValueReader
{
    public IReadOnlyDictionary<DataLinkValueAddress, DataLinkCurrentValue> Read(
        Document document,
        DataLinkWorkbook workbook)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (workbook == null) throw new ArgumentNullException(nameof(workbook));
        if (document.IsLinked) throw new InvalidOperationException("DataLink không đọc trực tiếp linked document.");

        var result = new Dictionary<DataLinkValueAddress, DataLinkCurrentValue>();
        foreach (var row in workbook.Rows)
        {
            var address = DataLinkValueAddress.From(row);
            var uniqueId = row.Parameter.IsType ? row.TypeUniqueId : row.ElementUniqueId;
            var target = string.IsNullOrWhiteSpace(uniqueId) ? null : document.GetElement(uniqueId);
            if (target == null)
            {
                result[address] = new DataLinkCurrentValue(false, false, false, false, string.Empty, string.Empty);
                continue;
            }

            var parameter = RevitParameterValueCodec.Resolve(target, row.Parameter);
            if (parameter == null)
            {
                result[address] = new DataLinkCurrentValue(true, false, false,
                    RevitDataLinkSnapshotReader.IsEditable(document, target), string.Empty, string.Empty);
                continue;
            }

            var described = RevitParameterValueCodec.Describe(parameter, row.Parameter.IsType);
            result[address] = new DataLinkCurrentValue(true, true, parameter.IsReadOnly,
                RevitDataLinkSnapshotReader.IsEditable(document, target), described.StorageKind,
                RevitParameterValueCodec.InternalValue(parameter));
        }
        return result;
    }
}
