using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT;
using System.Text.Json;

namespace skininjector_v2
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {

        private List<PackInfo> PackNameList_;

        private readonly string LEGACY_MINECRAFT_PATH = "%LocalAppData%\\Packages\\Microsoft.MinecraftUWP_8wekyb3d8bbwe\\LocalState\\premium_cache\\skin_packs";
        private readonly string LEGACY_MINECRAFT_PREVIEW_PATH = "%LocalAppData\\Packages\\Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe\\LocalState\\premium_cache\\skin_packs";
        private readonly string MINECRAFT_PATH = "%AppData%\\Minecraft Bedrock\\premium_cache\\skin_packs";
        private readonly string MINECRAFT_PREVIEW_PATH = "%AppData%\\Minecraft Bedrock Preview\\premium_cache\\skin_packs";

        private static string currentTargetPath = "";
        private static Boolean isPreviewLegacy = false;
        private static Boolean isMinecraftLegacy = false;
        private ContentDialog? _currentDialog;

        private static Boolean isPathSelected = false;
        private static Boolean isTargetPackSelected = false;

        private DesktopAcrylicController acrylicController;

        private SystemBackdropConfiguration backdropConfiguration;

        private Boolean isExistMinecraft = false;
        private Boolean isExistMinecraftPreview = false;

        public MainWindow()
        {
            this.InitializeComponent();
            Debug.WriteLine("起動した。");

            WindowHelper.SetMinSize(this, 1000, 700);
            EditionChangedBox.SelectedIndex = -1;
            EditionChangedBox.IsEnabled = false;
            InjectProgress.Value = 0;
            DeleteSkinDataBtn.IsEnabled = false;
            TrySetAcrylicBackdrop();

            UpdateSkinPackList();
        }


        private void TrySetAcrylicBackdrop()
        {
            if (DesktopAcrylicController.IsSupported())
            {
                acrylicController = new DesktopAcrylicController();
                backdropConfiguration = new SystemBackdropConfiguration();

                backdropConfiguration.IsInputActive = true;
                backdropConfiguration.Theme = SystemBackdropTheme.Default;

                acrylicController.SetSystemBackdropConfiguration(backdropConfiguration);
                acrylicController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());

                AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                AppWindow.TitleBar.BackgroundColor = Colors.Transparent;
                AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                SetTitleBar(TitleBar);


            }
        }

        private List<PackInfo> GetAllSkinPackData()
        {
            if (currentTargetPath == "")
            {
                if (Directory.Exists(Environment.ExpandEnvironmentVariables(LEGACY_MINECRAFT_PREVIEW_PATH)))
                {
                    currentTargetPath = Environment.ExpandEnvironmentVariables(LEGACY_MINECRAFT_PREVIEW_PATH);
                    isExistMinecraftPreview = true;
                    isPreviewLegacy = true;
                }
                else if (Directory.Exists(Environment.ExpandEnvironmentVariables(MINECRAFT_PREVIEW_PATH)))
                {
                    currentTargetPath = Environment.ExpandEnvironmentVariables(MINECRAFT_PREVIEW_PATH);
                    isExistMinecraftPreview = true;
                    isPreviewLegacy = false;

                }

                if (Directory.Exists(Environment.ExpandEnvironmentVariables(LEGACY_MINECRAFT_PATH)))
                {
                    currentTargetPath = Environment.ExpandEnvironmentVariables(LEGACY_MINECRAFT_PATH);
                    isExistMinecraft = true;
                    isMinecraftLegacy = true;
                }
                else
                if (Directory.Exists(Environment.ExpandEnvironmentVariables(MINECRAFT_PATH)))
                {
                    currentTargetPath = Environment.ExpandEnvironmentVariables(MINECRAFT_PATH);
                    isExistMinecraft = true;
                    isMinecraftLegacy = false;

                }
                MinecraftEdtionBoxItem.IsEnabled = isExistMinecraft;
                MinecraftPreviewEdtionBoxItem.IsEnabled = isExistMinecraftPreview;
                if (isExistMinecraft && isExistMinecraftPreview)
                {
                    EditionChangedBox.IsEnabled = true;
                }
                if (isExistMinecraft)
                {
                    EditionChangedBox.SelectedIndex = 0;
                }
                else if (isExistMinecraftPreview)
                {
                    EditionChangedBox.SelectedIndex = 1;
                }
            }
            string targetPath = currentTargetPath;

            var packList = new List<PackInfo>();

            var subFolders = Directory.GetDirectories(targetPath);

            foreach (var subFolder in subFolders)
            {
                string manifestPath = System.IO.Path.Combine(subFolder, "manifest.json");


                if (File.Exists(manifestPath))
                {
                    try
                    {
                        string content = File.ReadAllText(manifestPath);
                        var json = JsonObject.Parse(content);
                        if (JsonObject.Parse(content) == null)
                        {
                            continue;
                        }
                        string? packName = json["header"]?["name"]?.ToString();
                        if (packName != null)
                        {
                            packName = packName.Replace("\n", "").Replace("\r", "");
                            string textsFolder = System.IO.Path.Combine(subFolder, "texts");
                            if (Directory.Exists(textsFolder))
                            {
                                string japaneseFilePath = System.IO.Path.Combine(textsFolder, "ja_JP.lang");
                                string englishFilePath = System.IO.Path.Combine(textsFolder, "en_US.lang");
                                Regex regex = new Regex(@"^skinpack\.[^=\s]+=(.*\S.*)$", RegexOptions.Multiline);
                                if (File.Exists(japaneseFilePath))
                                {
                                    var text = File.ReadAllText(japaneseFilePath).ToString();
                                    MatchCollection matches = regex.Matches(text);
                                    if (matches.Count > 0)
                                    {
                                        packName = matches[0].Groups[1].Value.Trim();
                                    }
                                }
                                else if (File.Exists(englishFilePath))
                                {
                                    var text = File.ReadAllText(englishFilePath).ToString();
                                    MatchCollection matches = regex.Matches(text);
                                    if (matches.Count > 0)
                                    {
                                        packName = matches[0].Groups[1].Value.Trim();
                                    }
                                }
                            }
                            packList.Add(new PackInfo
                            {
                                FolderPath = subFolder,
                                PackName = packName
                            });
                        }
                        else
                        {
                            packName = "Unknown";
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        return packList;
                    }
                }
            }
            return packList;
        }

        private void PackItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var textBlock = sender as TextBlock;
            if (textBlock == null) return;

            var packInfo = textBlock.DataContext as PackInfo;
            if (packInfo == null) return;


            // メニュー作成
            var menu = new MenuFlyout();

            var openItem = new MenuFlyoutItem { Text = "フォルダを開く" };
            openItem.Click += (s, args) =>
            {

                if (Directory.Exists(packInfo.FolderPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = packInfo.FolderPath,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
            };

            var deleteItem = new MenuFlyoutItem { Text = "スキンデータを削除" };
            deleteItem.Click += (s, args) =>
            {
                if (packInfo.FolderPath != null)
                {
                    DeleteSkinDataByPackFolder(packInfo.FolderPath);
                }
            };

            menu.Items.Add(openItem);
            menu.Items.Add(deleteItem);

            // メニュー表示
            menu.ShowAt(textBlock, e.GetPosition(textBlock));
        }

        private void WindowDragEnter(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = e.DataView.GetStorageItemsAsync().AsTask().Result;
                if (items.Count > 0 && items[0] is StorageFolder)
                {
                    e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                }
            }
        }

        private async void WindowDrag(object sender, Microsoft.UI.Xaml.DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();

                if (items.Count > 0 && items[0] is StorageFolder folder)
                {
                    SelectedSkinPackPathBox.Text = folder.Path;
                    isPathSelected = true;
                }
            }
        }

        private void EditonChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.SelectedItem is not ComboBoxItem selectedItem) return;
            bool isMinecraft = selectedItem.Name == "MinecraftEdtionBoxItem";


            if (selectedItem != null && comboBox != null)
            {
                string? content = comboBox.SelectedValue.ToString();
                Debug.WriteLine(content);

                if (isMinecraft)
                {
                    ChangedPath.Text = Environment.ExpandEnvironmentVariables(MINECRAFT_PATH);
                    currentTargetPath = Environment.ExpandEnvironmentVariables(
                        isMinecraftLegacy ? LEGACY_MINECRAFT_PATH : MINECRAFT_PATH);
                }
                else
                {
                    ChangedPath.Text = Environment.ExpandEnvironmentVariables(MINECRAFT_PREVIEW_PATH);
                    currentTargetPath = Environment.ExpandEnvironmentVariables(
                        isPreviewLegacy ? LEGACY_MINECRAFT_PREVIEW_PATH : MINECRAFT_PREVIEW_PATH);
                }

                try
                {
                    isTargetPackSelected = false;
                    UpdateSkinPackList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private void UpdateSkinPackList()
        {
            try
            {
                PackNameList_ = GetAllSkinPackData();



                DeleteSkinDataBtn.IsEnabled = false;
                {
                    var listView = PackNameListView;
                    if (listView != null)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            listView.ItemsSource = PackNameList_;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async void Inject(object sender, RoutedEventArgs e)
        {
            await InjectAsync(sender, e);
        }

        private void PathChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null)
            {
                isPathSelected = false;
            }
            else
            {
                if (Directory.Exists(textBox.Text))
                {
                    isPathSelected = true;
                }
                else
                {
                    isPathSelected = false;
                }
            }
        }

        private void TargetPackChanged(object sender, SelectionChangedEventArgs e)
        {
            var listView = sender as ListView;
            if (listView != null)
            {
                if (listView.SelectedIndex != -1)
                {
                    isTargetPackSelected = true;
                    DeleteSkinDataBtn.IsEnabled = true;

                }
            }
        }

        private async Task InjectAsync(object sender, RoutedEventArgs e)
        {
            if (!isPathSelected)
            {
                this.ShowErrorMsg("置き換え先のスキンパックが選択されていません。");
                return;
            }

            if (!isTargetPackSelected)
            {
                this.ShowErrorMsg("置き換え元のスキンパックが選択されていません。");
                return;
            }

            if (!Directory.Exists(SelectedSkinPackPathBox.Text))
            {
                this.ShowErrorMsg("置き換え先のスキンパックのパスが存在しません。");
                InjectProgress.Value = 0;
                return;
            }

            try
            {


                string currentDiretory = Directory.GetCurrentDirectory();
                string tempDirectoryName = "skinpack";
                string sourcePath = SelectedSkinPackPathBox.Text;
                string? targetPath = PackNameList_[PackNameListView.SelectedIndex]?.FolderPath;

                if (targetPath == null)
                {
                    this.ShowErrorMsg("置き換え元のスキンパックのパスが存在しません。");
                    return;
                }
                await DisableUIElements();

                InjectProgress.Value = 0;
                // tempディレクトリの準備

                string tempSkinpackDirectryPath = Path.Combine(currentDiretory, tempDirectoryName);

                await PrepareTempDirectory(tempSkinpackDirectryPath, sourcePath);
                InjectProgress.Value = 20;

                if (Directory.Exists(targetPath))
                {
                    // 既存ファイルの削除
                    await DeleteExistingFiles(targetPath);
                    InjectProgress.Value = 30;

                    // 新しいファイルのコピー
                    await CopyTempFiles(tempSkinpackDirectryPath, targetPath);
                    InjectProgress.Value = 60;

                    await UpdateTargetPackName(targetPath, tempSkinpackDirectryPath);
                    InjectProgress.Value = 70;

                    // 暗号化処理
                    if (EncryptCheckBox.IsChecked == true)
                    {
                        await EncryptFiles(targetPath);
                    }

                    InjectProgress.Value = 100;
                    ShowMsg("処理が正常に終了しました。");
                }
            }
            catch (Exception ex)
            {
                this.ShowErrorMsg($"エラーが発生しました: {ex.Message}");
            }
            finally
            {
                EnableUIElements();
            }
        }
        private async Task DisableUIElements()
        {
            PackNameListView.IsEnabled = false;
            SelectedSkinPackPathBox.IsEnabled = false;
            SelectSkinPackPathBtn.IsEnabled = false;
            EncryptCheckBox.IsEnabled = false;
            EditionChangedBox.IsEnabled = false;
            InjectBtn.IsEnabled = false;
            DeleteSkinDataBtn.IsEnabled = false;
            await Task.Delay(50);
        }

        private void EnableUIElements()
        {
            PackNameListView.IsEnabled = true;
            SelectedSkinPackPathBox.IsEnabled = true;
            SelectSkinPackPathBtn.IsEnabled = true;
            EncryptCheckBox.IsEnabled = true;
            EditionChangedBox.IsEnabled = isExistMinecraft && isExistMinecraftPreview;
            InjectBtn.IsEnabled = true;
            DeleteSkinDataBtn.IsEnabled = true;
        }

        private async Task PrepareTempDirectory(string tempPath, string sourcePath)
        {
            try
            {
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                    await Task.Delay(50);
                }
                Directory.CreateDirectory(tempPath);
                await Task.Delay(50);

                Utils.CopyDirectory(sourcePath, tempPath);
            }
            catch (Exception ex)
            {
                ShowErrorMsg($"一時ディレクトリの準備中にエラーが発生しました: {ex.Message}");
            }
        }

        private async Task DeleteExistingFiles(string targetPath)
        {
            foreach (var filePath in Directory.GetFiles(targetPath))
            {
                string fileName = Path.GetFileName(filePath);
                if (fileName != "manifest.json")
                {
                    await Task.Run(() => File.Delete(filePath));
                }
            }

            foreach (var subDir in Directory.GetDirectories(targetPath))
            {
                await Task.Run(() => Directory.Delete(subDir, true));
            }
        }

        //private async Task ImportUUIDsFromTargetManifestAsync(string targetManifestPath, string sourceTargetPath)
        //{
        //    await Task.Run(() => {
        //        string targetFolderManifest = System.IO.Path.Combine(targetManifestPath, "manifest.json");
        //        if (!File.Exists(targetFolderManifest))
        //        {
        //            ShowErrorMsg("対象のスキンパックのmanifest.jsonが存在しません。");
        //            return;
        //        }
        //        string targetManifestData = File.ReadAllText(targetFolderManifest);
        //        var json = JsonObject.Parse(targetManifestData);
        //        if (json == null)
        //        {
        //            ShowErrorMsg("対象のスキンパックのmanifest.jsonが空っぽまたは無効です。");
        //            return;
        //        }
        //        string targetManifestHeaderUUID = json["header"]?["uuid"]?.ToString();
        //        string targetManifestModuleUUID = json["modules"]?[0]?["uuid"]?.ToString();

        //        Debug.WriteLine(targetManifestHeaderUUID, targetManifestModuleUUID);
        //        string sourceFolderManifestPath = System.IO.Path.Combine(sourceTargetPath, "manifest.json");
        //        if (!File.Exists(sourceFolderManifestPath))
        //        {
        //            ShowErrorMsg("元のスキンパックのmanifest.jsonが存在しません。");
        //            return;
        //        }
        //        string sourceFolderManifest = File.ReadAllText(sourceFolderManifestPath);
        //        var sourceFolderManifestJson = JsonObject.Parse(sourceFolderManifest);
        //        if (sourceFolderManifestJson == null)
        //        {
        //            ShowErrorMsg("元のスキンパックのmanifest.jsonが空っぽまたは無効です。");
        //            return;
        //        }

        //        if (sourceFolderManifestJson["header"] != null)
        //        {
        //            var headerNode = sourceFolderManifestJson["header"] as JsonObject;
        //            if (headerNode != null)
        //            {
        //                headerNode["uuid"] = targetManifestHeaderUUID;
        //            }
        //        }

        //        if (sourceFolderManifestJson["modules"] != null)
        //        {
        //            var modulesArray = sourceFolderManifestJson["modules"] as JsonArray;
        //            if (modulesArray != null && modulesArray.Count > 0)
        //            {
        //                var moduleNode = modulesArray[0] as JsonObject;
        //                if (moduleNode != null)
        //                {
        //                    moduleNode["uuid"] = targetManifestModuleUUID;
        //                }
        //            }
        //        }

        //        File.WriteAllText(sourceFolderManifestPath, sourceFolderManifestJson.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        //    });
        //}

        private async Task UpdateTargetPackName(string targetManifestPath, string sourceManifestPath)
        {
            await Task.Run(() =>
            {
                // --- 対象パックの manifest.json 読み込み ---
                string targetManifestFile = Path.Combine(targetManifestPath, "manifest.json");
                if (!File.Exists(targetManifestFile))
                {
                    ShowErrorMsg("対象のスキンパックの manifest.json が存在しません。");
                    return;
                }

                string targetManifestText = File.ReadAllText(targetManifestFile);
                JsonNode? targetManifestJson = JsonNode.Parse(targetManifestText);
                if (targetManifestJson == null)
                {
                    ShowErrorMsg("対象の manifest.json が空っぽまたは無効です。");
                    return;
                }

                // --- 元パックの manifest.json 読み込み ---
                string sourceManifestFile = Path.Combine(sourceManifestPath, "manifest.json");
                if (!File.Exists(sourceManifestFile))
                {
                    ShowErrorMsg("元のスキンパックの manifest.json が存在しません。");
                    return;
                }

                string sourceManifestText = File.ReadAllText(sourceManifestFile);
                JsonNode? sourceManifestJson = JsonNode.Parse(sourceManifestText);
                if (sourceManifestJson == null)
                {
                    ShowErrorMsg("元の manifest.json が空っぽまたは無効です。");
                    return;
                }

                // --- name を上書き ---
                string? newName = sourceManifestJson["header"]?["name"]?.ToString();
                if (newName == null)
                {
                    ShowErrorMsg("元の manifest.json に 'header.name' が存在しません。");
                    return;
                }

                if (targetManifestJson["header"] != null)
                {
                    targetManifestJson["header"]!["name"] = newName;
                }
                else
                {
                    ShowErrorMsg("対象の manifest.json に 'header' ノードが存在しません。");
                    return;
                }

                // --- 保存 ---
                File.WriteAllText(
                    targetManifestFile,
                    targetManifestJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
                );
            });
        }

        private async Task CopyTempFiles(string tempPath, string targetPath)
        {
            Utils.CopyDirectory(tempPath, targetPath, ["manifest.json"]);
        }

        private async Task EncryptFiles(string targetPath)
        {
            await Task.Run(() =>
            {
                Process encript = new();
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                encript.StartInfo.FileName = Path.Combine(currentDir, "MCEnc", "McEncryptor.exe");
                if (!File.Exists(Path.Combine(currentDir, "MCEnc", "McEncryptor.exe")))
                {
                    ShowErrorMsg("McEncryptor.exeが見つかりません。");
                    return;
                }
                encript.StartInfo.UseShellExecute = false;
                encript.StartInfo.RedirectStandardInput = true;
                encript.StartInfo.CreateNoWindow = true;

                encript.Start();
                encript.StandardInput.WriteLine(targetPath);
                encript.StandardInput.Close();

                encript.WaitForExit();
            });
        }

        private async void SelectSkinPackFolder(object sender, RoutedEventArgs e)
        {

            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);


            // ダイアログの開始ディレクトリを設定
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;

            // フォルダ選択ダイアログを表示
            StorageFolder selectedFolder = await folderPicker.PickSingleFolderAsync();
            if (selectedFolder != null)
            {
                SelectedSkinPackPathBox.Text = selectedFolder.Path;
                isPathSelected = true;
            }
        }

        private void DeleteSkinData(object sender, RoutedEventArgs e)
        {
            string? targetPath = PackNameList_[PackNameListView.SelectedIndex]?.FolderPath;
            if (targetPath == null)
            {
                this.ShowErrorMsg("削除するスキンパックのパスが存在しません。");
                return;
            }
            else
            {
                Directory.Delete(targetPath, true);
                InjectProgress.Value = 100;
                this.ShowMsg("削除しました。");
                UpdateSkinPackList();
                DeleteSkinDataBtn.IsEnabled = false;
                isTargetPackSelected = false;
            }
        }

        private void DeleteSkinDataByPackFolder(string folderPath)
        {
            string? targetPath = folderPath;
            if (targetPath == null)
            {
                this.ShowErrorMsg("削除するスキンパックのパスが存在しません。");
                return;
            }
            else
            {
                Directory.Delete(targetPath, true);
                InjectProgress.Value = 100;
                this.ShowMsg("削除しました。");
                UpdateSkinPackList();
                DeleteSkinDataBtn.IsEnabled = false;
                isTargetPackSelected = false;
            }
        }

        private async void ShowErrorMsg(String text)
        {
            if (_currentDialog != null) return;

            DispatcherQueue.TryEnqueue(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = "エラーが発生しました。",
                Content = $"{text}",
                CloseButtonText = "OK",
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            _currentDialog = dialog;
            InjectProgress.Value = 0;
            Debug.WriteLine(text);
            await dialog.ShowAsync();
            _currentDialog = null;

        });
        }

        private async void ShowMsg(String text)
        {
            if (_currentDialog != null)
                return; // すでに開いているので何もしない
            var dialog = new ContentDialog
            {
                Title = "成功",
                Content = $"{text}",
                CloseButtonText = "OK",
                XamlRoot = App.MainWindow.Content.XamlRoot

            };
            _currentDialog = dialog;
            await dialog.ShowAsync();
            _currentDialog = null;
            InjectProgress.Value = 0;
        }
    }

    public class PackInfo
    {
        public string? FolderPath { get; set; }
        public string? PackName { get; set; }
    }
}
