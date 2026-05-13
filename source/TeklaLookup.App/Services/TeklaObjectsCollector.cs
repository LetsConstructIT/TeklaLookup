using System;
using System.Collections.Generic;
using Tekla.Structures;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using TSModel = Tekla.Structures.Model;
using TSUI = Tekla.Structures.Model.UI;
using TSPicker = Tekla.Structures.Model.UI.Picker;

namespace TeklaLookup.App.Services;

/// <summary>
/// Reads model objects from the running Tekla Structures session. No writes — purely a query
/// surface for the lookup UI.
/// </summary>
public sealed class TeklaObjectsCollector
{
    private readonly TSModel.Model _model = new();

    /// <summary>
    /// Iterates every object in the model database.
    /// </summary>
    public IEnumerable<ModelObject> GetAllObjects()
    {
        var selector = _model.GetModelObjectSelector();
        var enumerator = selector.GetAllObjects();
        while (enumerator.MoveNext())
        {
            if (enumerator.Current is ModelObject mo)
                yield return mo;
        }
    }

    /// <summary>
    /// Iterates objects of any of the given CLR types (e.g. <see cref="Beam"/>, <see cref="ContourPlate"/>).
    /// Tekla resolves the appropriate <c>ModelObjectEnum</c> internally.
    /// </summary>
    public IEnumerable<ModelObject> GetObjectsOfTypes(Type[] types)
    {
        if (types == null || types.Length == 0) yield break;
        var selector = _model.GetModelObjectSelector();
        var enumerator = selector.GetAllObjectsWithType(types);
        while (enumerator.MoveNext())
        {
            if (enumerator.Current is ModelObject mo)
                yield return mo;
        }
    }

    /// <summary>
    /// Iterates objects of the given Tekla type enum value (e.g. BEAM, CONTOURPLATE).
    /// </summary>
    public IEnumerable<ModelObject> GetObjectsOfType(ModelObject.ModelObjectEnum type)
    {
        var selector = _model.GetModelObjectSelector();
        var enumerator = selector.GetAllObjectsWithType(type);
        while (enumerator.MoveNext())
        {
            if (enumerator.Current is ModelObject mo)
                yield return mo;
        }
    }

    /// <summary>
    /// Returns objects the user currently has selected in the Tekla UI.
    /// </summary>
    public IEnumerable<ModelObject> GetSelectedObjects()
    {
        var uiSelector = new TSUI.ModelObjectSelector();
        var enumerator = uiSelector.GetSelectedObjects();
        while (enumerator.MoveNext())
        {
            if (enumerator.Current is ModelObject mo)
                yield return mo;
        }
    }

    /// <summary>
    /// Resolves a session-scoped <see cref="Identifier"/> to its persistent GUID string.
    /// </summary>
    public string GetGuid(ModelObject modelObject)
        => _model.GetGUIDByIdentifier(modelObject.Identifier).ToString();

    /// <summary>
    /// Resolves an <see cref="Identifier"/> to its live <see cref="ModelObject"/>, or null when
    /// nothing in the model matches (stale identifier, wrong namespace, etc.).
    /// </summary>
    public ModelObject? ResolveIdentifier(Identifier identifier)
    {
        try { return _model.SelectModelObject(identifier); }
        catch { return null; }
    }

    /// <summary>Outcome of resolving a single user-provided token in <see cref="ResolveByTokens"/>.</summary>
    public sealed class ResolvedToken
    {
        public string Token { get; set; } = "";
        public ModelObject? Object { get; set; }
        public string? Reason { get; set; }
        public bool IsResolved => Object is not null;
    }

    /// <summary>
    /// Resolves a list of raw user-typed tokens (integer Identifier IDs or GUID strings) into
    /// live <see cref="ModelObject"/>s. Returns one result per token (resolved or not) so the
    /// caller can report misses to the user.
    /// </summary>
    public IEnumerable<ResolvedToken> ResolveByTokens(IEnumerable<string> tokens)
    {
        foreach (var raw in tokens)
        {
            var token = raw?.Trim();
            if (string.IsNullOrEmpty(token)) continue;

            ResolvedToken result;
            try
            {
                if (int.TryParse(token, out var idInt))
                {
                    var mo = _model.SelectModelObject(new Identifier(idInt));
                    result = new ResolvedToken
                    {
                        Token = token!,
                        Object = mo,
                        Reason = mo is null ? "No object with that ID" : null,
                    };
                }
                else
                {
                    var identifier = _model.GetIdentifierByGUID(token);
                    var mo = identifier.ID != 0 ? _model.SelectModelObject(identifier) : null;
                    result = new ResolvedToken
                    {
                        Token = token!,
                        Object = mo,
                        Reason = mo is null ? "No object with that GUID" : null,
                    };
                }
            }
            catch (Exception ex)
            {
                result = new ResolvedToken { Token = token!, Reason = ex.Message };
            }
            yield return result;
        }
    }

    /// <summary>
    /// Blocks the UI thread while the user picks a single object in the Tekla model viewport.
    /// Returns null if the user cancelled (Escape).
    /// </summary>
    public ModelObject? PickObject(string prompt = "Pick an object")
    {
        var picker = new TSPicker();
        try
        {
            return picker.PickObject(TSPicker.PickObjectEnum.PICK_ONE_OBJECT, prompt);
        }
        catch
        {
            // Tekla throws plain Exception on cancel — treat as no-pick.
            return null;
        }
    }

    /// <summary>
    /// Selects (and highlights) the given objects in the Tekla model viewport.
    /// </summary>
    public void SelectInModel(IEnumerable<ModelObject> objects)
    {
        var list = new System.Collections.ArrayList();
        foreach (var o in objects)
            list.Add(o);
        new TSUI.ModelObjectSelector().Select(list);
    }

    /// <summary>
    /// Blocks the UI thread while the user picks multiple objects (right-click to finish).
    /// </summary>
    public IEnumerable<ModelObject> PickObjects(string prompt = "Pick objects (right-click to finish)")
    {
        var picker = new TSPicker();
        ModelObjectEnumerator? enumerator;
        try
        {
            enumerator = picker.PickObjects(TSPicker.PickObjectsEnum.PICK_N_OBJECTS, prompt);
        }
        catch
        {
            yield break;
        }

        while (enumerator.MoveNext())
        {
            if (enumerator.Current is ModelObject mo)
                yield return mo;
        }
    }
}
