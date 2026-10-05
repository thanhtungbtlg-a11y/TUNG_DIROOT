using TungDiroot.Core.Diagnostics;

namespace TungDiroot.Addin.Diagnostics;

public interface ITungDirootLogger
{
    void Write(DiagnosticEvent diagnosticEvent);
}
