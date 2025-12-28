using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation.Diagnostics;
namespace skininjector_v2
{
    public class Injector
    {

        public static Action<int>? OnProgress;
        public static Action<string>? OnError;
        public static async Task ExecuteInjectionAsync(string sourcePath, string targetPath, bool isEncryptEnabled)
        {
            if (!TryValidateSkinPack(sourcePath, !isEncryptEnabled, out string error))
            {
                Logger.Error("Skin pack validation failed. Injection aborted.");
                throw new Exception(error);
            }
            OnProgress?.Invoke(10);
            await Task.Delay(50);

            Logger.Info("Skin pack validation succeeded. Proceeding with injection...");

            await CopyToTempFolder(sourcePath, targetPath);
            OnProgress?.Invoke(30);

            if (isEncryptEnabled)
            {
                await Task.Run(() => EncryptSkinPack(Path.Combine(Directory.GetCurrentDirectory(), "skinpack")));
            }
            OnProgress?.Invoke(60);

            await CleanupTargetFolder(targetPath);
            OnProgress?.Invoke(80);

            await CopyToTargetFolder(Path.Combine(Directory.GetCurrentDirectory(), "skinpack"), targetPath);
            OnProgress?.Invoke(100);

            Logger.Info("Skin pack injection completed successfully.");
        }

        public static async Task CopyToTempFolder(string sourcePath, string targetPath)
        {
            string tempPath = Path.Combine(Directory.GetCurrentDirectory(), "skinpack");

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

        public async static Task CleanupTargetFolder(string targetPath)
        {
            Logger.Info($"[Delete] Start cleanup: {targetPath}");

            if (!Directory.Exists(targetPath))
            {
                Logger.Warn($"[Delete] Target path does not exist: {targetPath}");
                return;
            }

            // 直下ファイル削除
            foreach (var filePath in Directory.GetFiles(targetPath))
            {
                string fileName = Path.GetFileName(filePath);

                try
                {
                    Logger.Info($"[Delete] Deleting file: {fileName}");

                    File.SetAttributes(filePath, FileAttributes.Normal);
                    File.Delete(filePath);
                    await Task.Yield();
                    Logger.Info($"[Delete] Deleted file: {fileName}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[Delete] Failed to delete file: {fileName}. ${ex}");
                }
            }

            // サブディレクトリ削除
            foreach (var subDir in Directory.GetDirectories(targetPath))
            {
                string dirName = Path.GetFileName(subDir);

                try
                {
                    Logger.Info($"[Delete] Deleting directory: {dirName}");

                    Directory.Delete(subDir, true);
                    await Task.Yield();

                    Logger.Info($"[Delete] Deleted directory: {dirName}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[Delete] Failed to delete directory: {dirName}. ${ex}");
                }
            }

            Logger.Info($"[Delete] Cleanup finished: {targetPath}");
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


        public static bool TryValidateSkinPack(string packPath, bool isEncrypted, out string error)
        {
            Debug.WriteLine(isEncrypted);
            error = "";

            if (!Directory.Exists(packPath)) error = "Pack directory does not exist.";

            string manifestPath = Path.Combine(packPath, "manifest.json");
            string skinsJsonPath = Path.Combine(packPath, "skins.json");

            if (!File.Exists(manifestPath)) error = "manifest.json does not exist.";
            if (!IsJsonValid(manifestPath)) error = "manifest.json is invalid.";
            if (!File.Exists(skinsJsonPath)) error = "skins.json does not exist.";

            if (isEncrypted) return true;

            if (!IsJsonValid(skinsJsonPath)) error = "skins.json is invalid.";

            if (!IsSkinValid(skinsJsonPath)) error = "skins.json content is invalid.";

            if (error != "")
            {
                Logger.Error(error);
                return false;
            }

            return true;
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

        private static bool IsSkinValid(string skinsJsonPath)
        {
            if (!File.Exists(skinsJsonPath))
                throw new Exception("Skins Json does not exists.");

            string jsonContent = File.ReadAllText(skinsJsonPath);
            string baseDir = Path.GetDirectoryName(skinsJsonPath)!;

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
                        Logger.Error($"Skin[{i}] missing required fields.");
                        return false;
                    }

                    // texture
                    string texturePath = Path.Combine(baseDir, s.Texture);
                    if (!File.Exists(texturePath))
                    {
                        Logger.Error($"Skin[{i}] texture not found: {s.Texture}");
                        return false;
                    }

                    // cape（任意）
                    if (!string.IsNullOrWhiteSpace(s.Cape))
                    {
                        string capePath = Path.Combine(baseDir, s.Cape);
                        if (!File.Exists(capePath))
                        {
                            Logger.Error($"Skin[{i}] cape not found: {s.Cape}");
                            return false;
                        }
                    }
                }

                Logger.Info("skins.json validation succeeded.");
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
