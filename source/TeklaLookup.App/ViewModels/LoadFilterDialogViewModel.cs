using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using TSAssembly = Tekla.Structures.Model.Assembly;
using Tekla.Structures.Model;
using TeklaLookup.App.Models;

namespace TeklaLookup.App.ViewModels;

public sealed class LoadFilterDialogViewModel : BaseViewModel
{
    public LoadFilterDialogViewModel()
    {
        Types = new ObservableCollection<ModelObjectTypeOption>
        {
            new("Beam",          typeof(Beam)),
            new("Contour plate", typeof(ContourPlate)),
            new("Poly beam",     typeof(PolyBeam)),
            new("Assembly",      typeof(TSAssembly)),
            new("Bolt group",    typeof(BoltGroup)),
            new("Weld",          typeof(BaseWeld)),
            new("Reinforcement", typeof(Reinforcement),  isSelected: false),
            new("Component",     typeof(BaseComponent),  isSelected: false),
            new("Reference model", typeof(ReferenceModel), isSelected: false),
        };

        SelectAllCommand   = new RelayCommand(_ => SetAll(true));
        ClearAllCommand    = new RelayCommand(_ => SetAll(false));
    }

    public ObservableCollection<ModelObjectTypeOption> Types { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ClearAllCommand { get; }

    public Type[] SelectedTeklaTypes => Types.Where(t => t.IsSelected).Select(t => t.TeklaType).ToArray();

    private void SetAll(bool value)
    {
        foreach (var t in Types) t.IsSelected = value;
    }
}
