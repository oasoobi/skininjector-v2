using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace skininjector_v2
{
    public static class HistoryManager
    {
        private static readonly string HistoryPath = "history.json";
        
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };
        public static List<HistoryItem> Load()
        {

            if (!File.Exists(HistoryPath))
                return new List<HistoryItem>();

            string json = File.ReadAllText(HistoryPath);

            return JsonSerializer.Deserialize<List<HistoryItem>>(json) ?? new();
        }

        public static void Save(List<HistoryItem> history)
        {

            string json = JsonSerializer.Serialize(history, JsonOptions);
            File.WriteAllText(HistoryPath, json);
        }

        public static void Add(HistoryItem item)
        {
            var history = Load();

            history.Insert(0, item);

            Save(history);
        }

        public static void Remove(Guid id)
        {
            var history = Load();
            history.RemoveAll(item => item.Id == id);
            Save(history);
        }

        public static void Clear()
        {
            Save(new List<HistoryItem>());
        }
    }

    public class HistoryItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Time { get; set; } = DateTime.Now;
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
        public string? Error { get; set; }

        public bool IsEncrypt { get; set; }

        public PackInfo SourcePack { get; set; } = new();
        public PackInfo TargetPack { get; set; } = new();
    }
}
