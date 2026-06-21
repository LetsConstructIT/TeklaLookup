using System.Collections.Generic;
using System.Reflection;
using Tekla.Structures.Catalogs;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

public sealed class ProfileItemExtensions : TeklaTypeExtension<ProfileItem>
{
    // Added in Tekla 2022 / 2024 respectively; resolved at runtime so the 2021-floor build still
    // surfaces them when running on a newer Tekla (and omits them on 2021). See OptionalTeklaApi.
    private static readonly MethodInfo? GetHighAccuracyCrossSection = OptionalTeklaApi.Method(typeof(ProfileItem), "GetHighAccuracyCrossSection");
    private static readonly MethodInfo? GetProfileItemSubTypes      = OptionalTeklaApi.Method(typeof(ProfileItem), "GetProfileItemSubTypes");

    protected override IEnumerable<PropertyEntry> ResolveCore(ProfileItem target)
    {
        if (GetHighAccuracyCrossSection is not null)
            yield return Entry("HighAccuracyCrossSection", () => GetHighAccuracyCrossSection.Invoke(target, null), "CrossSection");
        if (GetProfileItemSubTypes is not null)
            yield return Entry("ProfileItemSubTypes",      () => GetProfileItemSubTypes.Invoke(target, null),      "List<ProfileItemSubType>");
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
