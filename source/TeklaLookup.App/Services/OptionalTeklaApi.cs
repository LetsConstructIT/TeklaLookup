using System;
using System.Reflection;

namespace TeklaLookup.App.Services;

/// <summary>
/// Helpers for calling Tekla Open API members that exist only in newer Tekla versions.
///
/// The app is compiled against the 2021 Open API floor so a single TSEP installs and runs on
/// Tekla 2021+. A 2021-compiled assembly cannot reference members added later, so those members
/// can't be called directly. When the package is installed on a newer Tekla, the GAC-loaded Open
/// API assemblies expose the extra members; these helpers locate and invoke them by reflection, so
/// the one shipped binary lights up those capabilities at runtime on the versions that have them
/// (and silently omits them on 2021).
/// </summary>
internal static class OptionalTeklaApi
{
    /// <summary>
    /// The public, parameterless instance-or-static method <paramref name="name"/> on
    /// <paramref name="type"/>, or <c>null</c> if the running Tekla version doesn't define it.
    /// </summary>
    public static MethodInfo? Method(Type type, string name) =>
        type.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

    /// <summary>Whether the running Tekla version defines the parameterless method <paramref name="name"/>.</summary>
    public static bool Has(Type type, string name) => Method(type, name) is not null;
}
