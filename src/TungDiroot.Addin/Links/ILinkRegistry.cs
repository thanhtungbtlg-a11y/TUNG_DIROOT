using Autodesk.Revit.DB;
using TungDiroot.Core.Links;

namespace TungDiroot.Addin.Links;

public interface ILinkRegistry
{
    LinkRegistryModel Load(Document document);
    void Save(Document document, LinkRegistryModel registry);
    LinkRegistryModel RecoverFromViews(Document document);
}
