using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CursorContext.Services
{
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

        public static IReadOnlyList<string> GetWorkspaceFolderPaths()
        {
            if (!Directory.Exists(WorkspaceStoragePath))
                return [];

            var dirs = Directory.GetDirectories(WorkspaceStoragePath);
            var result = new List<string>(dirs.Length);
            foreach (var d in dirs)
                result.Add(d);
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
