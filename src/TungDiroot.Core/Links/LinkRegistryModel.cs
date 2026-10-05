using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TungDiroot.Core.Links;

public sealed class LinkRegistryModel
{
    public LinkRegistryModel(IEnumerable<LinkRecord> links)
    {
        Links = new ReadOnlyCollection<LinkRecord>((links ?? throw new ArgumentNullException(nameof(links))).ToArray());
    }

    public IReadOnlyList<LinkRecord> Links { get; }

    public LinkRecord? Find(Guid linkId) => Links.FirstOrDefault(link => link.LinkId == linkId);
}
