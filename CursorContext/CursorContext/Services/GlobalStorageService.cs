using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CursorContext.Services
{
    public sealed class BubbleItem
    {
        public string Text { get; }
        public string? ThinkingText { get; }
        public string? ToolFormerName { get; }
        public string FullJson { get; }

        public BubbleItem(string text, string fullJson, string? thinkingText = null, string? toolFormerName = null)
        {
            Text = text ?? "(no content)";
            FullJson = fullJson ?? "";
            ThinkingText = string.IsNullOrWhiteSpace(thinkingText) ? null : thinkingText;
            ToolFormerName = string.IsNullOrWhiteSpace(toolFormerName) ? null : toolFormerName;
        }
    }

    public static class GlobalStorageService
    {
        private static readonly string GlobalStorageDbPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "Cursor", "User", "globalStorage", "state.vscdb");

        public static IReadOnlyList<BubbleItem> GetConversationTexts(string composerId)
        {
            if (string.IsNullOrWhiteSpace(composerId) || !File.Exists(GlobalStorageDbPath))
                return [];

            try
            {
                var cs = new SqliteConnectionStringBuilder
                {
                    DataSource = GlobalStorageDbPath,
                    Mode = SqliteOpenMode.ReadOnly
                }.ToString();

                using var conn = new SqliteConnection(cs);
                conn.Open();

                var composerDataKey = "composerData:" + composerId;
                byte[]? composerBlob = null;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT value FROM cursorDiskKV WHERE key = $key";
                    cmd.Parameters.AddWithValue("$key", composerDataKey);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                        composerBlob = reader.GetFieldValue<byte[]>(0);
                }

                if (composerBlob is null)
                    return [];

                var composerJson = System.Text.Encoding.UTF8.GetString(composerBlob);
                using (var doc = JsonDocument.Parse(composerJson))
                {
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("fullConversationHeadersOnly", out var headers) || headers.ValueKind != JsonValueKind.Array)
                        return [];

                    var result = new List<BubbleItem>();
                    foreach (var header in headers.EnumerateArray())
                    {
                        if (!header.TryGetProperty("bubbleId", out var bubbleIdEl))
                            continue;
                        var bubbleId = bubbleIdEl.GetString();
                        if (string.IsNullOrWhiteSpace(bubbleId))
                            continue;

                        var bubbleKey = "bubbleId:" + composerId + ":" + bubbleId;
                        string text = "(no content)";
                        string? thinkingText = null;
                        string fullJson = "";
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT value FROM cursorDiskKV WHERE key = $key";
                            cmd.Parameters.AddWithValue("$key", bubbleKey);
                            using var reader = cmd.ExecuteReader();
                            if (reader.Read())
                            {
                                var bubbleBlob = reader.GetFieldValue<byte[]>(0);
                                fullJson = System.Text.Encoding.UTF8.GetString(bubbleBlob);
                                try
                                {
                                    using var bubbleDoc = JsonDocument.Parse(fullJson);
                                    var bubbleRoot = bubbleDoc.RootElement;
                                    if (bubbleRoot.TryGetProperty("text", out var textEl))
                                    {
                                        var t = textEl.GetString();
                                        text = string.IsNullOrWhiteSpace(t) ? "" : t;
                                    }
                                    if (bubbleRoot.TryGetProperty("thinking", out var thinkingEl) && thinkingEl.TryGetProperty("text", out var thinkingTextEl))
                                    {
                                        thinkingText = thinkingTextEl.GetString();
                                    }
                                    string? toolFormerName = null;
                                    if (bubbleRoot.TryGetProperty("toolFormerData", out var tfd) && tfd.TryGetProperty("name", out var nameEl))
                                    {
                                        toolFormerName = nameEl.GetString();
                                    }
                                    var hasOther = !string.IsNullOrWhiteSpace(thinkingText) || !string.IsNullOrWhiteSpace(toolFormerName);
                                    if (string.IsNullOrWhiteSpace(text) && !hasOther)
                                        text = "(empty)";
                                    result.Add(new BubbleItem(text, fullJson, thinkingText, toolFormerName));
                                    continue;
                                }
                                catch
                                {
                                    text = "(no content)";
                                }
                            }
                        }
                        result.Add(new BubbleItem(text, fullJson, thinkingText));
                    }
                    return result;
                }
            }
            catch
            {
                return [];
            }
        }

        /// <summary>Gets contextUsagePercent from composerData (allComposers.contextUsagePercent or root).</summary>
        public static double? GetContextUsagePercent(string composerId)
        {
            if (string.IsNullOrWhiteSpace(composerId) || !File.Exists(GlobalStorageDbPath))
                return null;
            try
            {
                var cs = new SqliteConnectionStringBuilder
                {
                    DataSource = GlobalStorageDbPath,
                    Mode = SqliteOpenMode.ReadOnly
                }.ToString();
                using var conn = new SqliteConnection(cs);
                conn.Open();
                var composerDataKey = "composerData:" + composerId;
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT value FROM cursorDiskKV WHERE key = $key";
                cmd.Parameters.AddWithValue("$key", composerDataKey);
                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return null;
                var composerJson = System.Text.Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(0));
                using var doc = JsonDocument.Parse(composerJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("allComposers", out var allComposers))
                {
                    if (allComposers.ValueKind == JsonValueKind.Object && allComposers.TryGetProperty("contextUsagePercent", out var pctEl) && pctEl.TryGetDouble(out var pct))
                        return pct;
                    if (allComposers.ValueKind == JsonValueKind.Array && allComposers.GetArrayLength() > 0 && allComposers[0].TryGetProperty("contextUsagePercent", out var firstPct) && firstPct.TryGetDouble(out var fp))
                        return fp;
                }
                if (root.TryGetProperty("contextUsagePercent", out var rootPct) && rootPct.TryGetDouble(out var rp))
                    return rp;
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
