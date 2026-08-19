namespace Fubar.Studio.UI.ViewModels;

/// <summary>
/// Aggregates the Left Pane's otherwise-unrelated concerns - the workspace tree
/// (<see cref="WorkspaceExplorer"/>), the theme switcher (<see cref="ThemeManager"/>), the
/// environment/secrets badge (<see cref="EnvironmentManager"/>), and the Environments/Auth
/// Profiles management groups (<see cref="EnvironmentsSection"/>/<see cref="AuthProfilesSection"/>)
/// - into one DataContext for <c>LeftPaneView</c> (LeftPane.md §4.1), without coupling those view
/// models to each other.
/// </summary>
public sealed class LeftPaneViewModel : ViewModelBase
{
    public WorkspaceExplorerViewModel WorkspaceExplorer { get; }

    public ThemeManagerViewModel ThemeManager { get; }

    public EnvironmentManagerViewModel EnvironmentManager { get; }

    public EnvironmentsSectionViewModel EnvironmentsSection { get; }

    public AuthProfilesSectionViewModel AuthProfilesSection { get; }

    public LeftPaneViewModel(
        WorkspaceExplorerViewModel workspaceExplorer,
        ThemeManagerViewModel themeManager,
        EnvironmentManagerViewModel environmentManager,
        EnvironmentsSectionViewModel environmentsSection,
        AuthProfilesSectionViewModel authProfilesSection)
    {
        WorkspaceExplorer = workspaceExplorer;
        ThemeManager = themeManager;
        EnvironmentManager = environmentManager;
        EnvironmentsSection = environmentsSection;
        AuthProfilesSection = authProfilesSection;
    }
}
