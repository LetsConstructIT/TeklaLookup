using System.Collections;
using System.Collections.Generic;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.Services.Extensions;

public sealed class ProjectInfoExtensions : TeklaTypeExtension<ProjectInfo>
{
    protected override IEnumerable<PropertyEntry> ResolveCore(ProjectInfo target)
    {
        yield return Entry("ProjectBasePoint",         () => ProjectInfo.GetProjectBasePoint(),         "BasePoint");
        yield return Entry("CurrentCoordsysBasePoint", () => ProjectInfo.GetCurrentCoordsysBasePoint(), "BasePoint");
        yield return Entry("BasePoints",               () => ProjectInfo.GetBasePoints(),               "List<BasePoint>");

        yield return Entry("IntegerUserProperties", () =>
        {
            var ht = new Hashtable();
            target.GetIntegerUserProperties(ref ht);
            return ht;
        }, "Hashtable<string,int>");

        yield return Entry("DoubleUserProperties", () =>
        {
            var ht = new Hashtable();
            target.GetDoubleUserProperties(ref ht);
            return ht;
        }, "Hashtable<string,double>");

        yield return Entry("StringUserProperties", () =>
        {
            var ht = new Hashtable();
            target.GetStringUserProperties(ref ht);
            return ht;
        }, "Hashtable<string,string>");
    }
}
