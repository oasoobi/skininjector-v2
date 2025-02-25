using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json.Nodes;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using System.Diagnostics;
using Windows.Storage.Pickers;
using System.Threading.Tasks;
using Microsoft.UI.Composition.SystemBackdrops;
using WinRT;
using Microsoft.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace skininjector_v2
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {

        private List<PackInfo> PackNameList_;
        private static string MINECRAFT_PATH = "%USERPROFILE%\\AppData\\Local\\Packages\\Microsoft.MinecraftUWP_8wekyb3d8bbwe\\LocalState\\premium_cache\\skin_packs";
        private static string MINECRAFT_PREVIEW_PATH = "%USERPROFILE%\\AppData\\Local\\Packages\\Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe\\LocalState\\premium_cache\\skin_packs";

        private static Boolean isPathSelected = false;
        private static Boolean isTargetPackSelected = false;
        private static Boolean isPreviewEdition = false;

        private DesktopAcrylicController acrylicController;

        private SystemBackdropConfiguration backdropConfiguration;

        private Boolean isExistMinecraft = false;
        private Boolean isExistMinecraftPreview = false;

        public MainWindow()
        {
            this.InitializeComponent();


            EditionChangedBox.SelectedIndex = 0;
            InjectProgress.Value = 0;
            DeleteSkinDataBtn.IsEnabled = false;
            TrySetAcrylicBackdrop();

            EditonChanged(EditionChangedBox, null);
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

        private static Boolean CheckSkinpackFolderExist(String Edition)
        {
            string targetPath;
            if (Edition == "minecraft preview")
            {
                targetPath = Environment.ExpandEnvironmentVariables(MINECRAFT_PREVIEW_PATH);
            }
            else
            {
                targetPath = Environment.ExpandEnvironmentVariables(MINECRAFT_PATH);
            }
            return Directory.Exists(targetPath);
        }

        private List<PackInfo> GetAllSkinPackData()
        {
            string targetPath;
            if (isPreviewEdition)
            {
                targetPath = Environment.ExpandEnvironmentVariables(MINECRAFT_PREVIEW_PATH);
            }
            else
            {
                targetPath = Environment.ExpandEnvironmentVariables(MINECRAFT_PATH);
            }

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
                        string? packName = json["header"]?["name"]?.ToString();

                        packName ??= "Unknown";
                        packList.Add(new PackInfo
                        {
                            FolderPath = subFolder,
                            PackName = packName
                        });

                    }
                    catch (Exception ex)
                    {
                        ShowErrorMsg(ex.Message);
                    }
                }
            }
            return packList;
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
            var selectedItem = comboBox?.SelectedItem as ComboBoxItem;

            isExistMinecraft = CheckSkinpackFolderExist("minecraft");
            isExistMinecraftPreview = CheckSkinpackFolderExist("minecraft preview");

            if (!isExistMinecraft)
            {
                MinecraftEdtionBoxItem.IsEnabled = false;
                Debug.WriteLine("be");
                EditionChangedBox.IsEnabled = false;
            }
            if (!isExistMinecraftPreview)
            {
                MinecraftPreviewEdtionBoxItem.IsEnabled = false;
                EditionChangedBox.IsEnabled = false;
                Debug.WriteLine("bep");
            }
            if (!isExistMinecraft && !isExistMinecraftPreview)
            {
                comboBox.SelectedIndex = -1;
                return;
            }

            if (selectedItem != null)
            {
                bool isPreview = selectedItem.Content.ToString() == "Minecraft Preview";

                if (!isPreview)
                {
                    isPreview = !isExistMinecraft && isExistMinecraftPreview;
                }

                if (isPreview)
                {
                    comboBox.SelectedIndex = 1;
                    ChangedPath.Text = Environment.ExpandEnvironmentVariables(MINECRAFT_PREVIEW_PATH);
                }
                else
                {
                    comboBox.SelectedIndex = 0;
                    ChangedPath.Text = Environment.ExpandEnvironmentVariables(MINECRAFT_PATH);
                }

                try
                {
                    isPreviewEdition = isPreview;


                    isTargetPackSelected = false;
                    UpdateSkinPackList();
                }
                catch (Exception ex)
                {
                    ShowErrorMsg(ex.Message);
                }
            }
        }

        private void UpdateSkinPackList()
        {
            try
            {
                PackNameList_ = GetAllSkinPackData();
                DeleteSkinDataBtn.IsEnabled = false;
                DispatcherQueue.TryEnqueue(() =>
                {
                    PackNameListView.ItemsSource = PackNameList_;
                });
            }
            catch (Exception ex)
            {
                ShowErrorMsg(ex.Message);
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
            await DisableUIElements();

            try
            {
                if (!Directory.Exists(SelectedSkinPackPathBox.Text))
                {
                    this.ShowErrorMsg("置き換え先のスキンパックのパスが存在しません。");
                    InjectProgress.Value = 0;
                    return;
                }

                string currentDiretory = Directory.GetCurrentDirectory();
                string tempDirectoryName = "skinpack";
                string sourcePath = SelectedSkinPackPathBox.Text;
                string? targetPath = PackNameList_[PackNameListView.SelectedIndex]?.FolderPath;

                if (targetPath == null)
                {
                    this.ShowErrorMsg("置き換え元のスキンパックのパスが存在しません。");
                    return;
                }

                InjectProgress.Value = 0;
                // tempディレクトリの準備
                string tempSkinpackDirectryPath = Path.Combine(currentDiretory, tempDirectoryName);
                await PrepareTempDirectory(tempSkinpackDirectryPath);
                InjectProgress.Value = 20;

                // ファイルのコピー
                await CopySourceFiles(sourcePath, tempSkinpackDirectryPath);
                InjectProgress.Value = 30;

                if (Directory.Exists(targetPath))
                {
                    // 既存ファイルの削除
                    await DeleteExistingFiles(targetPath);
                    InjectProgress.Value = 50;

                    // 新しいファイルのコピー
                    await CopyTempFiles(tempSkinpackDirectryPath, targetPath);
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
                InjectProgress.Value = 0;
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
            EditionChangedBox.IsEnabled = !isExistMinecraft && !isExistMinecraftPreview;
            InjectBtn.IsEnabled = true;
            DeleteSkinDataBtn.IsEnabled = true;
        }

        private async Task PrepareTempDirectory(string tempPath)
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
                await Task.Delay(50);
            }
            Directory.CreateDirectory(tempPath);
        }

        private async Task CopySourceFiles(string sourcePath, string tempPath)
        {
            foreach (var filePath in Directory.GetFiles(sourcePath))
            {
                string fileName = Path.GetFileName(filePath);
                if (fileName != "manifest.json")
                {
                    string destFilePath = Path.Combine(tempPath, fileName);
                    await Task.Run(() => File.Copy(filePath, destFilePath));
                }
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

        private async Task CopyTempFiles(string tempPath, string targetPath)
        {
            foreach (var filePath in Directory.GetFiles(tempPath))
            {
                string fileName = Path.GetFileName(filePath);
                string destFilePath = Path.Combine(targetPath, fileName);
                await Task.Run(() => File.Copy(filePath, destFilePath));
            }
        }

        private async Task EncryptFiles(string targetPath)
        {
            await Task.Run(() =>
            {
                Process encript = new();
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                encript.StartInfo.FileName = Path.Combine(currentDir, "MCEnc", "McEncryptor.exe");
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
                PackNameList_ = GetAllSkinPackData();
                PackNameListView.ItemsSource = PackNameList_;
                DeleteSkinDataBtn.IsEnabled = false;
                isTargetPackSelected = false;
            }
        }

        private async void ShowErrorMsg(String text)
        {
            var dialog = new ContentDialog
            {
                Title = "エラーが発生しました。",
                Content = $"{text}",
                CloseButtonText = "OK",
                XamlRoot = App.MainWindow.Content.XamlRoot

            };
            Debug.WriteLine(text);
            await dialog.ShowAsync();
        }

        private async void ShowMsg(String text)
        {
            var dialog = new ContentDialog
            {
                Title = "成功",
                Content = $"{text}",
                CloseButtonText = "OK",
                XamlRoot = App.MainWindow.Content.XamlRoot

            };
            await dialog.ShowAsync();
            InjectProgress.Value = 0;
        }
    }

    public class PackInfo
    {
        public string? FolderPath { get; set; }
        public string? PackName { get; set; }
    }
}
