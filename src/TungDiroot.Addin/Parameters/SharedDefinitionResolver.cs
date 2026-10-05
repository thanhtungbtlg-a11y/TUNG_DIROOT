using System;
using Autodesk.Revit.DB;
using TungDiroot.Core.Parameters;
using RevitApplication = Autodesk.Revit.ApplicationServices.Application;

namespace TungDiroot.Addin.Parameters;

public sealed class SharedDefinitionResolver
{
    public T Use<T>(RevitApplication application, string path, Func<DefinitionFile, T> action)
    {
        if (application == null) throw new ArgumentNullException(nameof(application));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Shared Parameter path is required.", nameof(path));
        if (action == null) throw new ArgumentNullException(nameof(action));

        return TemporaryValueScope.Use(
            () => application.SharedParametersFilename,
            value => application.SharedParametersFilename = value ?? string.Empty,
            path,
            () =>
            {
                var file = application.OpenSharedParameterFile()
                    ?? throw new InvalidOperationException("SHARED_PARAMETER_FILE_OPEN_FAILED: " + path);
                return action(file);
            });
    }
}
