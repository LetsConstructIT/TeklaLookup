using System.Collections.Generic;
using Tekla.Structures.Catalogs;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

public sealed class ProfileItemExtensions : TeklaTypeExtension<ProfileItem>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(ProfileItem target)
    {
        yield return Entry("HighAccuracyCrossSection", () => target.GetHighAccuracyCrossSection(), "CrossSection");
        yield return Entry("ProfileItemSubTypes",      () => target.GetProfileItemSubTypes(),      "List<ProfileItemSubType>");
        yield return Entry("IsProfileUserDefined",     () => target.IsProfileUserDefined(),        "bool");
        yield return Entry("IsProfileUserParametric",  () => target.IsProfileUserParametric(),     "bool");
    }
}

public sealed class ShapeItemExtensions : TeklaTypeExtension<ShapeItem>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(ShapeItem target)
    {
        yield return Entry("InstanceCount", () => target.GetInstanceCount(), "int");
    }
}

public sealed class ComponentItemExtensions : TeklaTypeExtension<ComponentItem>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(ComponentItem target)
    {
        yield return Entry("Version", () =>
        {
            var version = 0;
            return target.GetVersion(ref version) ? (object)version : null!;
        }, "int");
    }
}
