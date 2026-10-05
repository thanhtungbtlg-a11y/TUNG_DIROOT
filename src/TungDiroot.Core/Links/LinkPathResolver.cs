using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TungDiroot.Core.Links;

public enum PathMode
{
    Absolute,
    Relative
}

public sealed record StoredPathWarning(string Code, string Message);

public sealed class StoredPath
{
    public StoredPath(string value, PathMode mode, IReadOnlyList<StoredPathWarning>? warnings = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A stored path is required.", nameof(value));
        }

        Value = value;
        Mode = mode;
        Warnings = new ReadOnlyCollection<StoredPathWarning>((warnings ?? Enumerable.Empty<StoredPathWarning>()).ToArray());
    }

    public string Value { get; }

    public PathMode Mode { get; }

    public IReadOnlyList<StoredPathWarning> Warnings { get; }
}

public static class LinkPathResolver
{
    public const string ModelPathUnavailableWarningCode = "MODEL_PATH_UNAVAILABLE";

    public static StoredPath ToStoredPath(string sourcePath, string? modelPath, bool preferRelative)
    {
        var source = WindowsPath.ParseAbsolute(sourcePath, nameof(sourcePath));
        if (!preferRelative)
        {
            return new StoredPath(source.ToString(), PathMode.Absolute);
        }

        if (!TryGetStableModelFolder(modelPath, out var modelFolder))
        {
            return new StoredPath(source.ToString(), PathMode.Absolute, new[] { ModelPathUnavailable });
        }

        if (!string.Equals(source.Root, modelFolder.Root, StringComparison.OrdinalIgnoreCase))
        {
            return new StoredPath(source.ToString(), PathMode.Absolute);
        }

        return new StoredPath(GetRelativePath(modelFolder, source), PathMode.Relative);
    }

    public static string ResolveStoredPath(StoredPath stored, string? modelPath)
    {
        if (stored == null)
        {
            throw new ArgumentNullException(nameof(stored));
        }

        if (stored.Mode == PathMode.Absolute)
        {
            return WindowsPath.ParseAbsolute(stored.Value, nameof(stored)).ToString();
        }

        if (stored.Mode != PathMode.Relative)
        {
            throw new ArgumentOutOfRangeException(nameof(stored));
        }

        if (!TryGetStableModelFolder(modelPath, out var modelFolder))
        {
            throw new InvalidOperationException("A stable local model path is required to resolve a relative source path.");
        }

        return ResolveRelative(modelFolder, stored.Value).ToString();
    }

    private static StoredPathWarning ModelPathUnavailable { get; } = new StoredPathWarning(
        ModelPathUnavailableWarningCode,
        "The model path is unavailable or is not a stable local path.");

    private static bool TryGetStableModelFolder(string? modelPath, out WindowsPath modelFolder)
    {
        modelFolder = null!;
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return false;
        }

        try
        {
            var model = WindowsPath.ParseAbsolute(modelPath!, nameof(modelPath));
            if (model.IsUnc || model.Segments.Count == 0)
            {
                return false;
            }

            modelFolder = new WindowsPath(model.Root, model.Segments.Take(model.Segments.Count - 1), false);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string GetRelativePath(WindowsPath fromFolder, WindowsPath destination)
    {
        var shared = 0;
        while (shared < fromFolder.Segments.Count && shared < destination.Segments.Count &&
            string.Equals(fromFolder.Segments[shared], destination.Segments[shared], StringComparison.OrdinalIgnoreCase))
        {
            shared++;
        }

        var relative = Enumerable.Repeat("..", fromFolder.Segments.Count - shared)
            .Concat(destination.Segments.Skip(shared))
            .ToArray();
        return relative.Length == 0 ? "." : string.Join("\\", relative);
    }

    private static WindowsPath ResolveRelative(WindowsPath modelFolder, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || WindowsPath.IsRooted(value))
        {
            throw new ArgumentException("A relative stored path must not be rooted.", nameof(value));
        }

        var segments = new List<string>(modelFolder.Segments);
        foreach (var segment in value.Replace('/', '\\').Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    throw new ArgumentException("The relative stored path escapes its volume root.", nameof(value));
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            if (segment.IndexOf(':') >= 0)
            {
                throw new ArgumentException("A relative stored path must not contain a drive designator.", nameof(value));
            }

            segments.Add(segment);
        }

        return new WindowsPath(modelFolder.Root, segments, modelFolder.IsUnc);
    }

    private sealed class WindowsPath
    {
        public WindowsPath(string root, IEnumerable<string> segments, bool isUnc)
        {
            Root = root;
            Segments = new ReadOnlyCollection<string>(segments.ToArray());
            IsUnc = isUnc;
        }

        public string Root { get; }

        public IReadOnlyList<string> Segments { get; }

        public bool IsUnc { get; }

        public static bool IsAbsolute(string value)
        {
            if (value == null)
            {
                return false;
            }

            var normalized = value.Replace('/', '\\');
            return normalized.StartsWith("\\\\", StringComparison.Ordinal) ||
                (normalized.Length >= 3 && char.IsLetter(normalized[0]) && normalized[1] == ':' && normalized[2] == '\\');
        }

        public static bool IsRooted(string value)
        {
            if (value == null)
            {
                return false;
            }

            var normalized = value.Replace('/', '\\');
            return normalized.StartsWith("\\", StringComparison.Ordinal) ||
                (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':');
        }

        public static WindowsPath ParseAbsolute(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || !IsAbsolute(value))
            {
                throw new ArgumentException("The path must be an absolute Windows path.", parameterName);
            }

            var normalized = value.Replace('/', '\\');
            if (normalized.StartsWith("\\\\", StringComparison.Ordinal))
            {
                var parts = normalized.Substring(2).Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || parts[0] == "." || parts[0] == ".." || parts[1] == "." || parts[1] == "..")
                {
                    throw new ArgumentException("The UNC path must include a server and share.", parameterName);
                }

                return Create("\\\\" + parts[0] + "\\" + parts[1], parts.Skip(2), true, parameterName);
            }

            return Create(char.ToUpperInvariant(normalized[0]) + ":", normalized.Substring(3).Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries), false, parameterName);
        }

        public override string ToString()
        {
            if (Segments.Count == 0)
            {
                return Root + "\\";
            }

            return Root + "\\" + string.Join("\\", Segments);
        }

        private static WindowsPath Create(string root, IEnumerable<string> rawSegments, bool isUnc, string parameterName)
        {
            var segments = new List<string>();
            foreach (var segment in rawSegments)
            {
                if (segment == ".")
                {
                    continue;
                }

                if (segment == "..")
                {
                    if (segments.Count == 0)
                    {
                        throw new ArgumentException("The path escapes its volume root.", parameterName);
                    }

                    segments.RemoveAt(segments.Count - 1);
                    continue;
                }

                if (segment.IndexOf(':') >= 0)
                {
                    throw new ArgumentException("The path contains an invalid drive designator.", parameterName);
                }

                segments.Add(segment);
            }

            return new WindowsPath(root, segments, isUnc);
        }
    }
}
