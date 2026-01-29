using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CursorContext.Services;

namespace CursorContext.ViewModels
{
    public sealed partial class WorkspaceFolderItem
    {
        public string Path { get; }
        public string DisplayName { get; }

        public WorkspaceFolderItem(string path)
        {
            Path = path;
            DisplayName = System.IO.Path.GetFileName(path) ?? path;
        }
    }

    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ComposerNames))]
        private WorkspaceFolderItem? _selectedWorkspaceFolder;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ConversationTexts))]
        private ComposerItem? _selectedComposer;

        public ObservableCollection<WorkspaceFolderItem> WorkspaceFolders { get; } = [];
        public ObservableCollection<ComposerItem> ComposerNames { get; } = [];
        public ObservableCollection<BubbleItem> ConversationTexts { get; } = [];

        public MainWindowViewModel()
        {
            foreach (var path in WorkspaceStorageService.GetWorkspaceFolderPaths())
                WorkspaceFolders.Add(new WorkspaceFolderItem(path));
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
    }
}
