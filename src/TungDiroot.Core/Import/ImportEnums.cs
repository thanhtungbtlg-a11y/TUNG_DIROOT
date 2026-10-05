namespace TungDiroot.Core.Import;

public enum ImportMode
{
    RevitTable,
    Image
}

public enum TargetViewKind
{
    Drafting,
    Legend,
    Schedule
}

public enum UpdateBehavior
{
    Recreate,
    DataOnly
}

public enum ImageDpi
{
    Dpi150 = 150,
    Dpi300 = 300,
    Dpi600 = 600
}

public enum ImageRendererEngine
{
    Automatic,
    BuiltIn,
    ExcelHighFidelity
}
