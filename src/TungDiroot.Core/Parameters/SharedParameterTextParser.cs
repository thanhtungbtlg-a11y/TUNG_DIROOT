using System;
using System.Collections.Generic;
using System.IO;

namespace TungDiroot.Core.Parameters;

public sealed class SharedParameterTextParser
{
    public IReadOnlyList<SharedParameterDefinitionModel> Parse(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        var lines = File.ReadAllLines(path);
        var groups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < lines.Length; index++)
        {
            var fields = Fields(lines[index]);
            if (fields == null || !string.Equals(fields[0], "GROUP", StringComparison.OrdinalIgnoreCase)) continue;
            if (fields.Length < 3 || string.IsNullOrWhiteSpace(fields[1]) || string.IsNullOrWhiteSpace(fields[2]))
                throw Error(index, "GROUP record is malformed.");
            var id = fields[1].Trim();
            if (groups.ContainsKey(id)) throw Error(index, "Duplicate GROUP id " + id + ".");
            groups[id] = fields[2].Trim();
        }

        var definitions = new List<SharedParameterDefinitionModel>();
        var definitionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var definitionGuids = new HashSet<Guid>();
        for (var index = 0; index < lines.Length; index++)
        {
            var fields = Fields(lines[index]);
            if (fields == null || !string.Equals(fields[0], "PARAM", StringComparison.OrdinalIgnoreCase)) continue;
            if (fields.Length < 6 || string.IsNullOrWhiteSpace(fields[1]) || string.IsNullOrWhiteSpace(fields[2]) || string.IsNullOrWhiteSpace(fields[5]))
                throw Error(index, "PARAM record is malformed.");
            if (!Guid.TryParse(fields[1].Trim(), out var guid))
                throw Error(index, "PARAM GUID is invalid: " + fields[1] + ".");
            if (!definitionGuids.Add(guid))
                throw Error(index, "Duplicate PARAM GUID " + guid + ".");
            var groupId = fields[5].Trim();
            if (!groups.TryGetValue(groupId, out var groupName))
                throw Error(index, "PARAM references unknown GROUP id " + groupId + ".");
            var name = fields[2].Trim();
            var key = groupName + "\u001f" + name;
            if (!definitionKeys.Add(key))
                throw Error(index, "Duplicate PARAM definition " + groupName + "/" + name + ".");
            definitions.Add(new SharedParameterDefinitionModel(groupName, name, guid));
        }
        return definitions;
    }

    private static string[]? Fields(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith("*", StringComparison.Ordinal)) return null;
        var fields = line.Split('\t');
        if (fields.Length == 0 || string.Equals(fields[0], "META", StringComparison.OrdinalIgnoreCase)) return null;
        return fields;
    }

    private static InvalidDataException Error(int zeroBasedLine, string message) =>
        new($"Shared Parameter line {zeroBasedLine + 1}: {message}");
}
