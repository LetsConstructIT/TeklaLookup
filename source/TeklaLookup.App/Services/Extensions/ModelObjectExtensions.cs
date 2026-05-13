using System.Collections.Generic;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

public sealed class ModelObjectExtensions : TeklaTypeExtension<ModelObject>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(ModelObject target)
    {
        yield return Entry("Phase", () =>
        {
            Phase? phase = null;
            target.GetPhase(out phase);
            return phase;
        }, nameof(Phase));

        yield return Entry("Children", () => target.GetChildren(), "ModelObjectEnumerator");
    }
}
