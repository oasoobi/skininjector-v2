using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT;

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

            WindowHelper.SetMinSize(this, 1000, 700);

            EditionChangedBox.SelectedIndex = -1;
            EditionChangedBox.IsEnabled = false;
            InjectProgress.Value = 0;
            DeleteSkinDataBtn.IsEnabled = false;
            TrySetAcrylicBackdrop();

            UpdateSkinPackList();

            Injector.OnProgress = progess =>
            {
                _ = DispatcherQueue.TryEnqueue(() =>
                {
                    UpDateProgress(progess);
                });
            };

            Injector.OnError = message =>
            {
                _ = DispatcherQueue.TryEnqueue(() => ShowErrorMsg(message));
            };
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

            }

            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.BackgroundColor = Colors.Transparent;
            AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            SetTitleBar(TitleBar);
        }

        private List<PackInfo> GetAllSkinPackData()
        {
            if (currentTargetPath == "")
            {
                Logger.Info("Detecting Minecraft skin pack paths...");
                if (Directory.Exists(Environment.ExpandEnvironmentVariables(LEGACY_MINECRAFT_PREVIEW_PATH)))
                {
                    Logger.Info("Found legacy Minecraft Preview skin pack path.");
                    currentTargetPath = Environment.ExpandEnvironmentVariables(LEGACY_MINECRAFT_PREVIEW_PATH);
                    isExistMinecraftPreview = true;
                    isPreviewLegacy = true;
                }
                else if (Directory.Exists(Environment.ExpandEnvironmentVariables(MINECRAFT_PREVIEW_PATH)))
                {
                    Logger.Info("Found Minecraft Preview skin pack path.");
                    currentTargetPath = Environment.ExpandEnvironmentVariables(MINECRAFT_PREVIEW_PATH);
                    isExistMinecraftPreview = true;
                    isPreviewLegacy = false;

                }

                if (Directory.Exists(Environment.ExpandEnvironmentVariables(LEGACY_MINECRAFT_PATH)))
                {
                    Logger.Info("Found legacy Minecraft skin pack path.");
                    currentTargetPath = Environment.ExpandEnvironmentVariables(LEGACY_MINECRAFT_PATH);
                    isExistMinecraft = true;
                    isMinecraftLegacy = true;
                }
                else
                    if (Directory.Exists(Environment.ExpandEnvironmentVariables(MINECRAFT_PATH)))
                    {
                        Logger.Info("Found Minecraft skin pack path.");
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
                if (!isExistMinecraft && !isExistMinecraftPreview)
                {
                    Logger.Error("Minecraft skin pack path not found.");
                    ShowErrorMsg("Minecraftが見つかりませんでした。インストールされているかを確認してください。");
                }
            }

            string targetPath = currentTargetPath;

            var packList = new List<PackInfo>();

            var subFolders = Directory.GetDirectories(targetPath);

            Logger.Info("Getting All Skinpacks...");
            foreach (var subFolder in subFolders)
            {
                string manifestPath = System.IO.Path.Combine(subFolder, "manifest.json");
                Logger.Info($"Checking skinpack in folder: {subFolder}");

                if (!File.Exists(manifestPath)) continue;

                try
                {
                    string content = File.ReadAllText(manifestPath);
                    var json = JsonObject.Parse(content);
                    if (content == null || json == null) continue;

                    string? packName = json["header"]?["name"]?.GetValue<string>();
                    if (packName == null)
                    {
                        packName = "Unknown";
                        packList.Add(new PackInfo
                        {
                            FolderPath = subFolder,
                            PackName = packName
                        });
                        Logger.Info("Pack name not found in manifest, skipping...");
                        continue;
                    }

                    packName = packName.Replace("\n", "").Replace("\r", "");
                    string textsFolder = System.IO.Path.Combine(subFolder, "texts");
                    if (Directory.Exists(textsFolder))
                    {
                        Logger.Info("Looking for localized pack name...");
                        string japaneseFilePath = System.IO.Path.Combine(textsFolder, "ja_JP.lang");
                        string englishFilePath = System.IO.Path.Combine(textsFolder, "en_US.lang");
                        Regex regex = new(@"^skinpack\.[^=\s]+=(.*\S.*)$", RegexOptions.Multiline);
                        if (File.Exists(japaneseFilePath))
                        {
                            var text = File.ReadAllText(japaneseFilePath).ToString();
                            Logger.Info("Found Japanese localization file.");
                            MatchCollection matches = regex.Matches(text);
                            if (matches.Count > 0)
                            {
                                packName = matches[0].Groups[1].Value.Trim();
                                Logger.Info("Using Japanese localized pack name: " + packName);
                            }
                        }
                        else if (File.Exists(englishFilePath))
                        {
                            var text = File.ReadAllText(englishFilePath).ToString();
                            MatchCollection matches = regex.Matches(text);
                            if (matches.Count > 0)
                            {
                                packName = matches[0].Groups[1].Value.Trim();
                                Logger.Info("Using English localized pack name: " + packName);
                            }
                        }
                    }
                    Logger.Info($"Found pack: {packName}");
                    packList.Add(new PackInfo
                    {
                        FolderPath = subFolder,
                        PackName = packName
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(manifestPath + " is invalid: " + ex.Message);
                    return packList;
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
                    currentTargetPath = Environment.ExpandEnvironmentVariables(isPreviewLegacy ? LEGACY_MINECRAFT_PREVIEW_PATH : MINECRAFT_PREVIEW_PATH);
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
                            // 検索ボックスをクリア
                            SearchBox.Text = "";
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

        public void UpDateProgress(int progress)
        {
            InjectProgress.Value = progress;
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

        private async void Inject(object sender, RoutedEventArgs e)
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

            bool success = false;
            try
            {
                string currentDiretory = Directory.GetCurrentDirectory();

                string sourcePath = SelectedSkinPackPathBox.Text;
                string? targetPath = PackNameList_[PackNameListView.SelectedIndex]?.FolderPath;

                if (targetPath == null)
                {
                    this.ShowErrorMsg("置き換え元のスキンパックのパスが存在しません。");
                    return;
                }

                this.DisableUIElements();

                await Injector.ExecuteInjectionAsync(sourcePath, targetPath, EncryptCheckBox.IsChecked ?? true);
                success = true;
            }
            catch (Exception ex)
            {
                this.ShowErrorMsg($"エラーが発生しました: {ex.Message}");
            }
            finally
            {
                EnableUIElements();
                if (success)
                {
                    this.ShowMsg("処理が正常に終了しました。");
                }
            }
        }
        private void DisableUIElements()
        {
            PackNameListView.IsEnabled = false;
            SelectedSkinPackPathBox.IsEnabled = false;
            SelectSkinPackPathBtn.IsEnabled = false;
            EncryptCheckBox.IsEnabled = false;
            EditionChangedBox.IsEnabled = false;
            InjectBtn.IsEnabled = false;
            DeleteSkinDataBtn.IsEnabled = false;
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

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                PackNameListView.ItemsSource = PackNameList_;
            }
            else
            {
                var filtered = PackNameList_
                    .Where(p => p.PackName != null && p.PackName.ToLower().Contains(searchText))
                    .ToList();
                PackNameListView.ItemsSource = filtered;
            }

            // 選択状態をリセット
            isTargetPackSelected = false;
            DeleteSkinDataBtn.IsEnabled = false;
        }

        private void ReloadSkinPackList(object sender, RoutedEventArgs e)
        {
            UpdateSkinPackList();
        }
    }

    public class PackInfo
    {
        public string? FolderPath { get; set; }
        public string? PackName { get; set; }
    }
}
