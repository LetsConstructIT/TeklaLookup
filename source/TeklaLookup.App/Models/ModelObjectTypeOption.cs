using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TeklaLookup.App.Models;

/// <summary>
/// One row in the "Load by type" picker — a CLR type to query against Tekla's
/// <c>ModelObjectSelector.GetAllObjectsWithType(Type[])</c>, with a check-state for the UI.
/// </summary>
public sealed class ModelObjectTypeOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public ModelObjectTypeOption(string displayName, Type teklaType, bool isSelected = true)
    {
        DisplayName = displayName;
        TeklaType = teklaType;
        _isSelected = isSelected;
    }

    public string DisplayName { get; }
    public Type TeklaType { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
