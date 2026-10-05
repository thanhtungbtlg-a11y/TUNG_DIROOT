namespace TungDiroot.Core.DataLink;

using System;
using System.Security.Cryptography;
using System.Text;

public static class DataLinkWorkbookSchema
{
    public const int CurrentVersion = 1;
    public const string DataSheet = "DATA";
    public const string MetadataSheet = "_TUNG_META";

    internal static readonly string[] DataHeaders =
    {
        "Apply", "ElementId", "Category", "Family", "Type", "Parameter", "Original", "Edited", "Status"
    };

    internal static readonly string[] MetadataHeaders =
    {
        "SchemaVersion", "DocumentStamp", "CultureName", "ElementUniqueId", "TypeUniqueId",
        "ParameterStableId", "IsType", "StorageKind", "SpecId", "OriginalInternal", "RowChecksum", "ReadOnly"
    };

    internal static string ComputeChecksum(string[] metadataWithoutChecksum, string[] immutableVisibleValues)
    {
        var payload = new StringBuilder();
        foreach (var value in metadataWithoutChecksum)
        {
            var safe = value ?? string.Empty;
            payload.Append(safe.Length).Append(':').Append(safe).Append('|');
        }
        foreach (var value in immutableVisibleValues)
        {
            var safe = value ?? string.Empty;
            payload.Append(safe.Length).Append(':').Append(safe).Append('|');
        }
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString()))).Replace("-", string.Empty);
    }
}
