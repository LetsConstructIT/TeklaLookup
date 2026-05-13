using System.Collections.Generic;
using TSAssembly = Tekla.Structures.Model.Assembly;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

public sealed class AssemblyExtensions : TeklaTypeExtension<TSAssembly>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(TSAssembly target)
    {
        yield return Entry("MainPart",       () => target.GetMainPart(),       "Part");
        yield return Entry("Secondaries",    () => target.GetSecondaries(),    "ArrayList<Part>");
        yield return Entry("SubAssemblies",  () => target.GetSubAssemblies(),  "ArrayList<Assembly>");
    }
}
