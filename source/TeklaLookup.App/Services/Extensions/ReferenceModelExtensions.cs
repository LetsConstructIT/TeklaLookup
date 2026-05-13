using System.Collections.Generic;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

public sealed class ReferenceModelExtensions : TeklaTypeExtension<ReferenceModel>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(ReferenceModel target)
    {
        yield return Entry("Children",         () => target.GetChildren(),         "ModelObjectEnumerator");
        yield return Entry("ConvertedObjects", () => target.GetConvertedObjects(), "ModelObjectEnumerator");
        yield return Entry("CurrentRevision",  () => target.GetCurrentRevision(),  "Revision");
        yield return Entry("Revisions",        () => target.GetRevisions(),        "IEnumerable<Revision>");
    }
}

public sealed class ReferenceModelObjectExtensions : TeklaTypeExtension<ReferenceModelObject>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(ReferenceModelObject target)
    {
        yield return Entry("ReferenceModel", () => target.GetReferenceModel(), "ReferenceModel");
        yield return Entry("Father",         () => target.GetFather(),         "ModelObject");
    }
}
