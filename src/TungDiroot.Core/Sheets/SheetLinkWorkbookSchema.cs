namespace TungDiroot.Core.SheetTools;

public static class SheetLinkWorkbookSchema
{
    public const string SheetsSheet = "SHEETS";
    public const string ViewsSheet = "VIEWS";
    public const string RevisionsSheet = "REVISION";

    public static readonly string[] SheetHeaders =
    {
        "APPLY", "ACTION", "UNIQUE_ID", "SHEET_NUMBER", "SHEET_NAME", "TITLE_BLOCK_TYPE", "REVISION_SEQUENCES"
    };

    public static readonly string[] ViewHeaders =
    {
        "APPLY", "ACTION", "UNIQUE_ID", "VIEW_NAME", "VIEW_KIND", "LEVEL_NAME", "VIEW_TEMPLATE",
        "SHEET_NUMBER", "CENTER_X_MM", "CENTER_Y_MM"
    };

    public static readonly string[] RevisionHeaders =
    {
        "APPLY", "ACTION", "UNIQUE_ID", "REVISION_KEY", "SEQUENCE_NUMBER", "DESCRIPTION", "REVISION_DATE",
        "ISSUED", "ISSUED_BY", "ISSUED_TO", "VISIBILITY"
    };
}
