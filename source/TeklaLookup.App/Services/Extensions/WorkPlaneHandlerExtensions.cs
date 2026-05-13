using System.Collections.Generic;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

public sealed class WorkPlaneHandlerExtensions : TeklaTypeExtension<WorkPlaneHandler>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(WorkPlaneHandler target)
    {
        yield return Entry("CurrentTransformationPlane",
            () => target.GetCurrentTransformationPlane(),
            "TransformationPlane");
    }
}
