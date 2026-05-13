using System.Collections.Generic;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;
using TSModel = Tekla.Structures.Model.Model;

namespace TeklaLookup.App.Services.Extensions;

/// <summary>
/// Surfaces the root entry points of the running Tekla model session — useful when the user
/// wants to walk from the model itself down into project info, phases, work plane, etc.
/// </summary>
public sealed class ModelExtensions : TeklaTypeExtension<TSModel>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(TSModel target)
    {
        yield return Entry("ConnectionStatus", () => target.GetConnectionStatus(), "Boolean");
        yield return Entry("ModelInfo",        () => target.GetInfo(),             "ModelInfo");
        yield return Entry("ProjectInfo",      () => target.GetProjectInfo(),      "ProjectInfo");
        yield return Entry("Phases",           () => target.GetPhases(),           "ArrayList<Phase>");
        yield return Entry("WorkPlaneHandler", () => target.GetWorkPlaneHandler(), "WorkPlaneHandler");
        yield return Entry("ClashCheckHandler",() => target.GetClashCheckHandler(),"ClashCheckHandler");
        yield return Entry("Catalogs",         () => new Tekla.Structures.Catalogs.CatalogHandler(), "CatalogHandler");
        yield return Entry("TeklaStructuresInfo",  () => new TeklaStructuresInfoSnapshot(), "TeklaStructuresInfoSnapshot");
        yield return Entry("TeklaStructuresFiles", () => new Tekla.Structures.TeklaStructuresFiles(), "TeklaStructuresFiles");
    }
}
