using CommunityToolkit.Mvvm.ComponentModel;

namespace Fubar.Studio.UI.ViewModels;

/// <summary>One editable row in the Environment Editor's variable grid (Key/Value/Secret/Description).</summary>
public partial class EnvironmentVariableRowViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Key { get; set; } = "";

    [ObservableProperty]
    public partial string Value { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSecret { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = "";
}
