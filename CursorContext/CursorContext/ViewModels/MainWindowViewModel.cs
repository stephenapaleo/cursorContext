using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CursorContext.Services;

namespace CursorContext.ViewModels
{
    public sealed partial class WorkspaceFolderItem
    {
        public string Path { get; }
        public string DisplayName { get; }

        public WorkspaceFolderItem(string path, string? displayName = null)
        {
            Path = path;
            DisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName : (System.IO.Path.GetFileName(path) ?? path);
        }
    }

    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ComposerNames))]
        private WorkspaceFolderItem? _selectedWorkspaceFolder;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ConversationTexts)), NotifyPropertyChangedFor(nameof(ContextUsagePercentText))]
        private ComposerItem? _selectedComposer;

        public string ContextUsagePercentText =>
            SelectedComposer is null ? "" : GlobalStorageService.GetContextUsagePercent(SelectedComposer.ComposerId) is { } pct
                ? $"Context usage: {pct:F0}%"
                : "";

        public ObservableCollection<WorkspaceFolderItem> WorkspaceFolders { get; } = [];
        public ObservableCollection<ComposerItem> ComposerNames { get; } = [];
        public ObservableCollection<BubbleItem> ConversationTexts { get; } = [];

        public MainWindowViewModel()
        {
            foreach (var entry in WorkspaceStorageService.GetWorkspaceFolderEntries())
                WorkspaceFolders.Add(new WorkspaceFolderItem(entry.Path, entry.DisplayName));
        }

        partial void OnSelectedWorkspaceFolderChanged(WorkspaceFolderItem? value)
        {
            ComposerNames.Clear();
            if (value is null) return;
            foreach (var item in WorkspaceStorageService.GetComposers(value.Path))
                ComposerNames.Add(item);
        }

        partial void OnSelectedComposerChanged(ComposerItem? value)
        {
            ConversationTexts.Clear();
            if (value is null || string.IsNullOrWhiteSpace(value.ComposerId)) return;
            foreach (var item in GlobalStorageService.GetConversationTexts(value.ComposerId))
                ConversationTexts.Add(item);
        }

        [RelayCommand]
        private void Refresh()
        {
            var currentPath = SelectedWorkspaceFolder?.Path;
            var currentComposerId = SelectedComposer?.ComposerId;
            WorkspaceFolders.Clear();
            foreach (var entry in WorkspaceStorageService.GetWorkspaceFolderEntries())
                WorkspaceFolders.Add(new WorkspaceFolderItem(entry.Path, entry.DisplayName));
            if (currentPath is not null)
            {
                var match = System.Linq.Enumerable.FirstOrDefault(WorkspaceFolders, w => w.Path == currentPath);
                SelectedWorkspaceFolder = match;
            }
            if (currentComposerId is not null && SelectedWorkspaceFolder is not null)
            {
                var composerMatch = System.Linq.Enumerable.FirstOrDefault(ComposerNames, c => c.ComposerId == currentComposerId);
                SelectedComposer = composerMatch;
            }
        }
    }
}
