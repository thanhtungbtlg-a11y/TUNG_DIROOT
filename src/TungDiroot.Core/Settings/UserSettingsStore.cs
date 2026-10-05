using System;
using System.IO;
using Newtonsoft.Json;

namespace TungDiroot.Core.Settings;

public sealed class UserSettingsStore
{
    private readonly string _path;

    public UserSettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TUNG_DIROOT", "settings.json");
    }

    public UserSettings Load()
    {
        if (!File.Exists(_path)) return new UserSettings();
        try { return JsonConvert.DeserializeObject<UserSettings>(File.ReadAllText(_path)) ?? new UserSettings(); }
        catch (JsonException)
        {
            var corrupt = _path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            File.Move(_path, corrupt);
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        var directory = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("SETTINGS_PATH_INVALID");
        Directory.CreateDirectory(directory);
        var temp = _path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temp, JsonConvert.SerializeObject(settings, Formatting.Indented));
        try
        {
            if (File.Exists(_path)) File.Replace(temp, _path, null);
            else File.Move(temp, _path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
