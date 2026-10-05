using System;
using System.IO;
using System.Text;
using TungDiroot.Core.Diagnostics;

namespace TungDiroot.Addin.Diagnostics;

public sealed class RollingFileLogger : ITungDirootLogger
{
    private const long MaxBytes = 2L * 1024L * 1024L;
    private const int MaxFiles = 5;
    private readonly object _gate = new();
    private readonly string _directory;

    public RollingFileLogger(string? directory = null) => _directory = directory ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TUNG_DIROOT", "Logs");

    public void Write(DiagnosticEvent diagnosticEvent)
    {
        if (diagnosticEvent == null) throw new ArgumentNullException(nameof(diagnosticEvent));
        lock (_gate)
        {
            Directory.CreateDirectory(_directory);
            var current = Path.Combine(_directory, "TUNG_DIROOT.log");
            if (File.Exists(current) && new FileInfo(current).Length >= MaxBytes) Rotate(current);
            File.AppendAllText(current, diagnosticEvent.ToJson() + Environment.NewLine, new UTF8Encoding(false));
        }
    }

    private void Rotate(string current)
    {
        var oldest = current + "." + MaxFiles;
        if (File.Exists(oldest)) File.Delete(oldest);
        for (var index = MaxFiles - 1; index >= 1; index--)
        {
            var source = current + "." + index;
            if (File.Exists(source)) File.Move(source, current + "." + (index + 1));
        }
        File.Move(current, current + ".1");
    }
}
