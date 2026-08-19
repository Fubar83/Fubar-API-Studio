using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fubar.Studio.Core.Models;
using Fubar.Studio.Core.Secrets;
using Fubar.Studio.Core.Workspaces;

namespace Fubar.Studio.UI.ViewModels;

/// <summary>
/// Opened in the main canvas (in place of the Request Editor) when a Left Pane Environments row is
/// clicked for editing. Secret-flagged variables are stored via <see cref="ISecretStoreService"/>
/// (never written to the environment's JSON file - see <see cref="AppVariable"/>'s doc comment);
/// this editor reads/writes the real value there transparently.
/// </summary>
public partial class EnvironmentEditorViewModel : ViewModelBase
{
    private readonly Workspace _workspace;
    private readonly IEnvironmentStore _workspaceService;
    private readonly ISecretStoreService _secretStore;
    private readonly StatusLogViewModel _statusLog;
    private readonly string _environmentId;

    public EnvironmentEditorViewModel(
        WorkspaceEnvironment environment,
        Workspace workspace,
        IEnvironmentStore workspaceService,
        ISecretStoreService secretStore,
        StatusLogViewModel statusLog)
    {
        _workspace = workspace;
        _workspaceService = workspaceService;
        _secretStore = secretStore;
        _statusLog = statusLog;
        _environmentId = environment.Id;

        Name = environment.Name;

        foreach (var variable in environment.Variables)
        {
            var value = variable.IsSecret
                ? _secretStore.TryGetSecret(workspace.WorkspaceId, variable.Key) ?? ""
                : variable.Value ?? "";

            Rows.Add(new EnvironmentVariableRowViewModel
            {
                Key = variable.Key,
                Value = value,
                IsSecret = variable.IsSecret,
                Description = variable.Description ?? "",
            });
        }
    }

    /// <summary>Used by MainViewModel to highlight this environment's row in the Left Pane while
    /// it's the active canvas surface.</summary>
    public string EnvironmentId => _environmentId;

    /// <summary>The workspace this environment belongs to - used by MainViewModel to clear the
    /// main canvas when that workspace's tab is closed.</summary>
    public Workspace Workspace => _workspace;

    [ObservableProperty]
    public partial string Name { get; set; }

    public ObservableCollection<EnvironmentVariableRowViewModel> Rows { get; } = [];

    /// <summary>Raised after a successful Save - the Left Pane's Environments section refreshes from it.</summary>
    public event Action? Saved;

    [RelayCommand]
    private void AddRow() => Rows.Add(new EnvironmentVariableRowViewModel());

    [RelayCommand]
    private void RemoveRow(EnvironmentVariableRowViewModel? row)
    {
        if (row is not null)
        {
            Rows.Remove(row);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var variables = new List<AppVariable>();
        foreach (var row in Rows.Where(r => !string.IsNullOrWhiteSpace(r.Key)))
        {
            if (row.IsSecret)
            {
                _secretStore.SetSecret(_workspace.WorkspaceId, row.Key, row.Value);
            }

            variables.Add(new AppVariable
            {
                Key = row.Key,
                Value = row.IsSecret ? null : row.Value,
                IsSecret = row.IsSecret,
                Description = string.IsNullOrEmpty(row.Description) ? null : row.Description,
            });
        }

        var model = new WorkspaceEnvironment { Id = _environmentId, Name = Name, Variables = variables };

        try
        {
            await _workspaceService.SaveEnvironmentAsync(_workspace.RootPath, model);
            _statusLog.Log($"Saved environment \"{Name}\".");
            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            _statusLog.Log($"Failed to save environment \"{Name}\": {ex.Message}");
        }
    }
}
