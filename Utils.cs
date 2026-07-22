using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace skininjector_v2
{
    public static class Utils {
        public static void CopyDirectory(string sourceDir, string destinationDir, IEnumerable<string>? excludeFileNames = null)
        {
            excludeFileNames ??= Enumerable.Empty<string>();

            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);

                if (excludeFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Copy(file, Path.Combine(destinationDir, fileName), true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(subDir);
                CopyDirectory(subDir, Path.Combine(destinationDir, dirName), excludeFileNames);
            }
        }

        public static PackInfo? GetPackInfoFromManifest(string manifestPath)
        {
            if (!File.Exists(manifestPath))
                return null;

            try
            {
                string content = File.ReadAllText(manifestPath);
                var json = JsonObject.Parse(content);

                if (json == null)
                    return null;

                string folderPath = Path.GetDirectoryName(manifestPath) ?? "";

                string? packName = json["header"]?["name"]?.GetValue<string>();
                string? packUUID = json["header"]?["uuid"]?.GetValue<string>();

                if (string.IsNullOrWhiteSpace(packName))
                {
                    packName = "Unknown";
                }
                else
                {
                    packName = packName.Replace("\n", "").Replace("\r", "");

                    string textsFolder = Path.Combine(folderPath, "texts");

                    if (Directory.Exists(textsFolder))
                    {
                        Regex regex = new(@"^skinpack\.[^=\s]+=(.*\S.*)$", RegexOptions.Multiline);

                        string japaneseFilePath = Path.Combine(textsFolder, "ja_JP.lang");
                        string englishFilePath = Path.Combine(textsFolder, "en_US.lang");

                        string? langFile = null;

                        if (File.Exists(japaneseFilePath))
                            langFile = japaneseFilePath;
                        else if (File.Exists(englishFilePath))
                            langFile = englishFilePath;

                        if (langFile != null)
                        {
                            string text = File.ReadAllText(langFile);
                            Match match = regex.Match(text);

                            if (match.Success)
                                packName = match.Groups[1].Value.Trim();
                        }
                    }
                }

                return new PackInfo
                {
                    FolderPath = folderPath,
                    PackName = packName,
                    PackUUID = packUUID
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"{manifestPath} is invalid: {ex.Message}");
                return null;
            }
        }
    }

}
