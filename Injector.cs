using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation.Diagnostics;
namespace skininjector_v2
{
    public class Injector
    {

        public static Action<int>? OnProgress;
        public static Action<string>? OnError;
        public static Func<string, Task<bool>>? OnConfirm;
        public static async Task ExecuteInjectionAsync(string sourcePath, string targetPath, bool isEncryptEnabled)
        {
            var (isValid, error) = await TryValidateSkinPackAsync(sourcePath, !isEncryptEnabled);
            if (!isValid)
            {
                Logger.Error("Skin pack validation failed. Injection aborted.");
                throw new Exception(error);
            }
            OnProgress?.Invoke(10);
            await Task.Delay(50);

            Logger.Info("Skin pack validation succeeded. Proceeding with injection...");

            await CopyToTempFolder(sourcePath, targetPath);
            OnProgress?.Invoke(30);
            await Task.Delay(50);

            if (isEncryptEnabled)
            {
                await Task.Run(() => EncryptSkinPack(Path.Combine(Directory.GetCurrentDirectory(), "skinpack")));
            }
            OnProgress?.Invoke(80);

            SwapSkinPack(
                Path.Combine(Directory.GetCurrentDirectory(), "skinpack"),
                targetPath
            );
            await Task.Delay(50);
            OnProgress?.Invoke(100);
            Logger.Info("Skin pack injection completed successfully.");
        }

        public static async Task CopyToTempFolder(string sourcePath, string targetPath)
        {
            string tempPath = Path.Combine(Directory.GetCurrentDirectory(), "skinpack");

            Logger.Info(tempPath);

            if (!Directory.Exists(tempPath)) Directory.CreateDirectory(tempPath);

            foreach (var dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, tempPath));
                await Task.Yield();
            }
            foreach (var newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(newPath);

                if (fileName != "manifest.json") {
                    File.Copy(newPath, newPath.Replace(sourcePath, tempPath), true);
                    await Task.Yield();
                } 
            }
            Logger.Info($"Copied skin pack to temporary folder: {tempPath}");

            string targetDiretoryManifestPath = Path.Combine(targetPath, "manifest.json");
            if (File.Exists(targetDiretoryManifestPath))
            {
                File.Copy(targetDiretoryManifestPath, Path.Combine(tempPath, "manifest.json"), true);
                await Task.Yield();
                Logger.Info("Copied existing manifest.json to temporary folder.");
            }
            else
            {
                Logger.Error("No existing manifest.json found in target directory.");
                throw new Exception("No existing manifest json found in target directory.");
            }

            var GetPackTranslateName = new Regex(@"^^skinpack\.[^.=\s]+(?!\.by)=(.+)$");

            string targetDiretoryLanguagePath = Path.Combine(targetPath, "texts/en_US.lang");

            Logger.Info($"Looking for language file at: {targetDiretoryLanguagePath}");

            if (File.Exists(targetDiretoryLanguagePath))
            {
                var targetLines = await File.ReadAllLinesAsync(targetDiretoryLanguagePath);
                string? skinpackName = null;

                foreach (var line in targetLines)
                {
                    if (line.StartsWith("skinpack.") && !line.Contains(".by="))
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            skinpackName = parts[1];
                            break;
                        }
                    }
                }

                string tempLanguageDir = Path.Combine(tempPath, "texts");

                if (skinpackName == null)
                {
                    throw new Exception("skinpack name not found in target lang");
                }
                var skinpackLineRegex = new Regex(@"^skinpack\.(?!.*\.by=)[^=]+=.+");

                foreach (var langFile in Directory.GetFiles(tempLanguageDir, "*.lang"))
                {
                    var lines = await File.ReadAllLinesAsync(langFile);
                    var updated = lines.Select(line =>
                    {
                        if (skinpackLineRegex.IsMatch(line))
                        {
                            var key = line.Split('=', 2)[0];
                            return $"{key}={skinpackName}";
                        }
                        return line;
                    }).ToArray(); ;
                    await File.WriteAllLinesAsync(langFile, updated);
                }
            }
            else
            {
                Logger.Warn("No existing en_US.lang found in target directory.");
            }
        }

        public static void EncryptSkinPack(string tempPath)
        {
            Process encript = new();
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            encript.StartInfo.FileName = Path.Combine(currentDir, "MCEnc", "McEncryptor.exe");
            if (!File.Exists(Path.Combine(currentDir, "MCEnc", "McEncryptor.exe")))
            {
                Logger.Error("McEncryptor.exe not found.");
                throw new FileNotFoundException("McEncryptor.exe not found.");
            }
            encript.StartInfo.UseShellExecute = false;
            encript.StartInfo.RedirectStandardInput = true;
            encript.StartInfo.CreateNoWindow = true;

            encript.Start();
            encript.StandardInput.WriteLine(tempPath);
            encript.StandardInput.Close();

            encript.WaitForExit();
        }

        public static void SwapSkinPack(string preparedPath, string targetPath)
        {
            string backupPath = targetPath + "_old_" + Guid.NewGuid();

            if (Directory.Exists(targetPath))
            {
                Directory.Move(targetPath, backupPath);
            }

            Directory.Move(preparedPath, targetPath);

            // 後始末（失敗しても致命傷じゃない）
            try
            {
                Directory.Delete(backupPath, true);
            }
            catch { }
        }

        public static async Task CopyToTargetFolder(string tempPath, string targetPath)
        {
            foreach (var filePath in Directory.GetFiles(tempPath))
            {
                string fileName = Path.GetFileName(filePath);
                string destFilePath = Path.Combine(targetPath, fileName);
                File.Copy(filePath, destFilePath);
                await Task.Yield();
            }
            Logger.Info($"Copied skin pack to target folder: {targetPath}");
        }


        public static async Task<(bool isValid, string error)> TryValidateSkinPackAsync(string packPath, bool isEncrypted)
        {
            Debug.WriteLine(isEncrypted);
            string error = "";

            if (!Directory.Exists(packPath)) error = "Pack directory does not exist.";

            string manifestPath = Path.Combine(packPath, "manifest.json");
            string skinsJsonPath = Path.Combine(packPath, "skins.json");

            if (!File.Exists(manifestPath)) error = "manifest.json does not exist.";
            if (!IsJsonValid(manifestPath)) error = "manifest.json is invalid.";
            if (!File.Exists(skinsJsonPath)) error = "skins.json does not exist.";

            if (isEncrypted) return (true, "");

            if (!IsJsonValid(skinsJsonPath)) error = "skins.json is invalid.";

            if (!await IsSkinValidAsync(skinsJsonPath)) error = "skins.json content is invalid.";

            if (error != "")
            {
                Logger.Error(error);
                return (false, error);
            }

            return (true, "");
        }

        private static bool IsJsonValid(string jsonPath)
        {
            try
            {
                string jsonContent = File.ReadAllText(jsonPath);
                System.Text.Json.JsonDocument.Parse(jsonContent);
                return true;
            }
            catch (System.Text.Json.JsonException)
            {
                return false;
            }
        }

        private static async Task<bool> IsSkinValidAsync(string skinsJsonPath)
        {
            if (!File.Exists(skinsJsonPath))
                throw new Exception("Skins Json does not exists.");

            string jsonContent = File.ReadAllText(skinsJsonPath);
            string baseDir = Path.GetDirectoryName(skinsJsonPath)!;

            var validCount = 0;
            var warnings = new List<string>();

            try
            {
                var data = JsonSerializer.Deserialize<Root>(jsonContent);

                if (data is null || data.Skins.Length == 0)
                {
                    Logger.Error("skins array is missing or empty.");
                    return false;
                }

                for (int i = 0; i < data.Skins.Length; i++)
                {
                    var s = data.Skins[i];

                    if (string.IsNullOrWhiteSpace(s.LocalizationName) ||
                        string.IsNullOrWhiteSpace(s.Geometry) ||
                        string.IsNullOrWhiteSpace(s.Texture) ||
                        string.IsNullOrWhiteSpace(s.Type))
                    {
                        var msg = $"Skin[{i}] missing required fields.";
                        Logger.Warn(msg);
                        warnings.Add(msg);
                        continue;
                    }

                    // texture
                    string texturePath = Path.Combine(baseDir, s.Texture);
                    if (!File.Exists(texturePath))
                    {
                        var msg = $"Skin{i} texture not found: {s.Texture}";
                        Logger.Warn(msg);
                        warnings.Add(msg);
                        continue;
                    }

                    // cape（任意）
                    if (!string.IsNullOrWhiteSpace(s.Cape))
                    {
                        string capePath = Path.Combine(baseDir, s.Cape);
                        if (!File.Exists(capePath))
                        {
                            var msg = $"Skin[{i}] cape not found: {s.Cape}";
                            Logger.Warn(msg);
                            warnings.Add(msg);
                            continue;
                        }
                    }

                    validCount++;
                }

                if (validCount <= 0)
                {
                    Logger.Error("No valid skins found in skins.json.");
                    return false;
                }

                Logger.Info($"Total valid skins found: {validCount}");
                Logger.Info($"Total skins processed: {data.Skins.Length}");
                Logger.Info("skins.json content is valid.");

                // 警告がある場合はダイアログで続けるかを選択
                if (warnings.Count > 0 && OnConfirm != null)
                {
                    var warningMessage = $"以下の警告があります:\n\n{string.Join("\n", warnings)}\n\n有効なスキン: {validCount}/{data.Skins.Length}\n\n続行しますか？";
                    var shouldContinue = await OnConfirm(warningMessage);
                    if (!shouldContinue)
                    {
                        Logger.Info("User cancelled injection due to warnings.");
                        return false;
                    }
                }

                return true;
            }
            catch (JsonException ex)
            {
                Logger.Error($"Invalid JSON format: {ex.Message}");
                return false;
            }
        }

    }

    record Root(
        [property: JsonPropertyName("skins")]
    Skin[] Skins
);

    record Skin(
        [property: JsonPropertyName("localization_name")]
    string LocalizationName,

        [property: JsonPropertyName("geometry")]
    string Geometry,

        [property: JsonPropertyName("texture")]
    string Texture,

        [property: JsonPropertyName("type")]
    string Type,

        [property: JsonPropertyName("cape")]
    string? Cape
    );
}
