using CommunityToolkit.Mvvm.Input;

namespace GroundNotes.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private void ToggleTagFilter()
    {
        IsTagFilterExpanded = !IsTagFilterExpanded;
    }
}
