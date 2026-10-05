using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace TungDiroot.Core.Profiles;

public sealed record ProfileLoadResult<T>(string Path, T? Profile, string? ErrorCode, string? ErrorMessage)
    where T : class
{
    public bool IsSuccess => Profile != null && ErrorCode == null;

    public static ProfileLoadResult<T> Loaded(string path, T profile) => new(path, profile, null, null);
    public static ProfileLoadResult<T> Failed(string path, string errorCode, string? message) =>
        new(path, null, errorCode, message);
}

public sealed class JsonProfileStore<T> where T : class
{
    private readonly string _directory;
    private readonly Func<T, string> _nameSelector;
    private readonly Func<T, string?> _validator;

    public JsonProfileStore(string directory, Func<T, string> nameSelector, Func<T, string?>? validator = null)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("PROFILE_DIRECTORY_REQUIRED", nameof(directory));
        _directory = directory;
        _nameSelector = nameSelector ?? throw new ArgumentNullException(nameof(nameSelector));
        _validator = validator ?? (_ => null);
    }

    public IReadOnlyList<ProfileLoadResult<T>> LoadAll()
    {
        if (!Directory.Exists(_directory)) return Array.Empty<ProfileLoadResult<T>>();

        return Directory.GetFiles(_directory, "*.json")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(LoadOne)
            .ToArray();
    }

    public void Save(T profile)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        var validationError = _validator(profile);
        if (validationError != null) throw new InvalidOperationException(validationError);

        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, SafeFileName(_nameSelector(profile)) + ".json");
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temp, JsonConvert.SerializeObject(profile, Formatting.Indented));
        try
        {
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public bool Delete(string profileName)
    {
        var path = Path.Combine(_directory, SafeFileName(profileName) + ".json");
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    private ProfileLoadResult<T> LoadOne(string path)
    {
        try
        {
            var profile = JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
            if (profile == null) return ProfileLoadResult<T>.Failed(path, "PROFILE_JSON_EMPTY", "Profile JSON không có dữ liệu.");
            var validationError = _validator(profile);
            return validationError == null
                ? ProfileLoadResult<T>.Loaded(path, profile)
                : ProfileLoadResult<T>.Failed(path, validationError, "Profile không tương thích hoặc thiếu dữ liệu.");
        }
        catch (JsonException ex)
        {
            return ProfileLoadResult<T>.Failed(path, "PROFILE_JSON_INVALID", ex.Message);
        }
        catch (IOException ex)
        {
            return ProfileLoadResult<T>.Failed(path, "PROFILE_READ_FAILED", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ProfileLoadResult<T>.Failed(path, "PROFILE_READ_DENIED", ex.Message);
        }
    }

    private static string SafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("PROFILE_NAME_REQUIRED");
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? throw new InvalidOperationException("PROFILE_NAME_INVALID") : sanitized;
    }
}
