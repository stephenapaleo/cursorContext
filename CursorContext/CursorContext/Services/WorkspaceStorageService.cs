using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CursorContext.Services
{
    public sealed record WorkspaceFolderEntry(string Path, string DisplayName);

    public sealed class ComposerItem
    {
        public string Name { get; }
        public string ComposerId { get; }

        public ComposerItem(string name, string composerId)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "(Unnamed)" : name;
            ComposerId = composerId ?? string.Empty;
        }
    }

    public static class WorkspaceStorageService
    {
        private static readonly string WorkspaceStoragePath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "Cursor", "User", "workspaceStorage");

        public static IReadOnlyList<WorkspaceFolderEntry> GetWorkspaceFolderEntries()
        {
            if (!Directory.Exists(WorkspaceStoragePath))
                return [];

            var dirs = Directory.GetDirectories(WorkspaceStoragePath);
            var result = new List<WorkspaceFolderEntry>(dirs.Length);
            foreach (var directoryPath in dirs)
            {
                var displayName = Path.GetFileName(directoryPath) ?? directoryPath;
                var workspaceJsonPath = System.IO.Path.Combine(directoryPath, "workspace.json");
                if (File.Exists(workspaceJsonPath))
                {
                    try
                    {
                        var json = File.ReadAllText(workspaceJsonPath);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("folder", out var folderEl))
                        {
                            var folder = folderEl.GetString();
                            if (!string.IsNullOrWhiteSpace(folder) && System.Uri.TryCreate(folder, System.UriKind.Absolute, out var uri))
                                displayName = uri.LocalPath;
                            else if (!string.IsNullOrWhiteSpace(folder))
                                displayName = folder;
                        }
                    }
                    catch
                    {
                        // keep directory name as display name
                    }
                }
                result.Add(new WorkspaceFolderEntry(directoryPath, displayName));
            }
            return result;
        }

        public static IReadOnlyList<ComposerItem> GetComposers(string workspaceFolderPath)
        {
            if (string.IsNullOrWhiteSpace(workspaceFolderPath))
                return [];

            var dbPath = Path.Combine(workspaceFolderPath, "state.vscdb");
            if (!File.Exists(dbPath))
                return [];

            try
            {
                var cs = new SqliteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    Mode = SqliteOpenMode.ReadOnly
                }.ToString();

                using var conn = new SqliteConnection(cs);
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT value FROM ItemTable WHERE key = 'composer.composerData'";
                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return [];

                var blob = reader.GetFieldValue<byte[]>(0);
                var json = System.Text.Encoding.UTF8.GetString(blob);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("allComposers", out var allComposers) || allComposers.ValueKind != JsonValueKind.Array)
                    return [];

                var result = new List<ComposerItem>();
                foreach (var composer in allComposers.EnumerateArray())
                {
                    var name = composer.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                    var composerId = composer.TryGetProperty("composerId", out var idEl) ? idEl.GetString() : null;
                    result.Add(new ComposerItem(name ?? "(Unnamed)", composerId ?? ""));
                }
                return result;
            }
            catch
            {
                return [];
            }
        }
    }
}
