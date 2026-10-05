using System.Collections.Generic;
using TungDiroot.Core.Excel;
using TungDiroot.Core.Layout;
using Xunit;

namespace TungDiroot.Core.Tests.Layout;

public sealed class StyleMappingProfileTests
{
    [Fact]
    public void Mapping_is_resolved_by_deterministic_excel_style_key()
    {
        var mapping = new RevitStyleMapping("text-id", "line-id");
        var profile = new StyleMappingProfile(new Dictionary<string, RevitStyleMapping>
        {
            [CellStyleKey.Create(CellStyleModel.Default)] = mapping
        });

        Assert.Equal(mapping, profile.Resolve(CellStyleModel.Default));
    }

    [Fact]
    public void Line_mapping_is_resolved_by_style_and_color_key()
    {
        var mapping = new RevitStyleMapping(null, "line-id");
        var profile = new StyleMappingProfile(new Dictionary<string, RevitStyleMapping>
        {
            [LineStyleKey.Create("thin", "FF112233")] = mapping
        });

        Assert.Equal(mapping, profile.ResolveLine("thin", "FF112233"));
        Assert.Null(profile.ResolveLine("thick", "FF112233"));
    }
}
