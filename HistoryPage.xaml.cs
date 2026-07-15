using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Services.Maps;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace skininjector_v2
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HistoryPage : Page
    {
        public ObservableCollection<HistoryItem> History { get; set; } = new();

        public ICommand CopyCommand { get; }

        private ContentDialog? _currentDialog;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            History.Clear();

            foreach (var item in HistoryManager.Load())
            {
                History.Add(item);
            }

            try
            {
                var history = HistoryManager.Load();
                History = new ObservableCollection<HistoryItem>(history);

                if (history.Count == 0)
                {
                    EmptyView.Visibility = Visibility.Visible;
                    HistoryScrollViewer.Visibility = Visibility.Collapsed;
                    DeleteAllHistoryButton.IsEnabled = false;
                }
                else
                {
                    EmptyView.Visibility = Visibility.Collapsed;
                    HistoryScrollViewer.Visibility = Visibility.Visible;
                    DeleteAllHistoryButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText("error.txt", ex.ToString());
            }
        }

        public HistoryPage()
        {
            InitializeComponent();

            DataContext = this;

            CopyCommand = new CommandHandler<string>(CopyText);

            Injector.OnError = message =>
            {
                _ = DispatcherQueue.TryEnqueue(() => ShowErrorMsg(message));
            };

            Injector.OnConfirm = async (message) =>
            {
                var tcs = new TaskCompletionSource<bool>();
                _ = DispatcherQueue.TryEnqueue(async () =>
                {
                    var dialog = new ContentDialog
                    {
                        Title = "警告",
                        Content = message,
                        PrimaryButtonText = "続行",
                        CloseButtonText = "キャンセル",
                        XamlRoot = App.MainWindow.Content.XamlRoot
                    };
                    var result = await dialog.ShowAsync();
                    tcs.SetResult(result == ContentDialogResult.Primary);
                });
                return await tcs.Task;
            };
        }

        private void BackToHomeButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(HomePage));
        }

        private void DeleteAllHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            HistoryManager.Clear();
            this.Frame.Navigate(typeof(HistoryPage));
        }

        private void CopyText(object parameter)
        {
            if (parameter is string text)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(text);
                Clipboard.SetContent(dataPackage);
            }
            
        }

        private void OpenFolder(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
        button.Tag is string path)
            {
                if (path == null)
                {
                    ShowErrorMsg("該当フォルダが削除されているため開けませんでした。");
                    return;
                }
                if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                } else
                {
                    ShowErrorMsg("該当フォルダが削除されているため開けませんでした。");
                }
            }
                
        }



        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element &&
        element.DataContext is HistoryItem item)
            {
                if (!Directory.Exists(item.TargetPack.FolderPath))
                {
                    this.ShowErrorMsg("すでにスキンデータを復元する準備ができています。マイクラの更衣室でスキンパックを再読み込みすることで復元されます。(復元するまでSkinInjector上には表示されません。)");
                    return;
                }
                else
                {
                    Directory.Delete(item.TargetPack.FolderPath, true);
                    HistoryManager.Remove(item.Id);
                    this.ShowMsg("スキンデータを復元する準備ができました。マイクラの更衣室でスキンパックを再読み込みすることで復元されます。(復元するまでSkinInjector上には表示されず、この履歴も削除されます。)");
                    this.Frame.Navigate(typeof(HistoryPage));
                }
            }
        }

        private async void ReRun_Click(object sender, SplitButtonClickEventArgs e)
        {
            if (sender is SplitButton button &&
        button.DataContext is HistoryItem item)
            {
                bool success = false;
                try
                {

                    ContentRoot.IsEnabled = false;
                    if (!Directory.Exists(item.TargetPack.FolderPath))
                    {
                      ShowErrorMsg("対象のフォルダが削除されています。");
                        return;
                    } 
                    if (!Directory.Exists(item.SourcePack.FolderPath))
                    {
                        ShowErrorMsg("置き換え元のフォルダが削除されています。");
                        return;
                    }
                    await Injector.ExecuteInjectionAsync(item.SourcePack.FolderPath, item.TargetPack.FolderPath, item.IsEncrypt);

                    success = true;
                }
                catch (Exception ex)
                {
                    this.ShowErrorMsg($"エラーが発生しました: {ex.Message}");
                }
                finally
                {
                    ContentRoot.IsEnabled = true;
                    this.Frame.Navigate(typeof(HistoryPage));

                    if (success)
                    {
                        this.ShowMsg("処理が正常に終了しました。");
                    }
                }
            }
        }

        private async void ReRunNoEnc_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element &&
        element.DataContext is HistoryItem item)
            {
                bool success = false;
                try
                {

                    ContentRoot.IsEnabled = false;
                    if (!Directory.Exists(item.TargetPack.FolderPath))
                    {
                        ShowErrorMsg("対象のフォルダが削除されています。");
                        return;
                    }
                    if (!Directory.Exists(item.SourcePack.FolderPath))
                    {
                        ShowErrorMsg("置き換え元のフォルダが削除されています。");
                        return;
                    }
                    await Injector.ExecuteInjectionAsync(item.SourcePack.FolderPath, item.TargetPack.FolderPath, true);
                    success = true;
                }
                catch (Exception ex)
                {
                    this.ShowErrorMsg($"エラーが発生しました: {ex.Message}");
                }
                finally
                {
                    ContentRoot.IsEnabled = true;
                    this.Frame.Navigate(typeof(HistoryPage));

                    if (success)
                    {
                        this.ShowMsg("処理が正常に終了しました。");
                    }
                }
            }
        }
        private async void ReRunEnc_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element &&
        element.DataContext is HistoryItem item)
            {                

                bool success = false;
                try
                {

                    ContentRoot.IsEnabled = false;
                    if (!Directory.Exists(item.TargetPack.FolderPath))
                    {
                        ShowErrorMsg("対象のフォルダが削除されています。");
                        return;
                    }
                    if (!Directory.Exists(item.SourcePack.FolderPath))
                    {
                        ShowErrorMsg("置き換え元のフォルダが削除されています。");
                        return;
                    }
                    await Injector.ExecuteInjectionAsync(item.SourcePack.FolderPath, item.TargetPack.FolderPath, false);
                    success = true;
                }
                catch (Exception ex)
                {
                    this.ShowErrorMsg($"エラーが発生しました: {ex.Message}");
                }
                finally
                {
                    ContentRoot.IsEnabled = true;
                    this.Frame.Navigate(typeof(HistoryPage));

                    if (success)
                    {
                        this.ShowMsg("処理が正常に終了しました。");
                    }
                }

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
        }

        private class CommandHandler<T> : ICommand
        {
            private readonly Action<T?> _action;

            public CommandHandler(Action<T?> action)
            {
                _action = action;
            }

            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter)
            {
                _action((T?)parameter);
            }
        }

        private void RemoveHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
        button.Tag is Guid id)
            {
                HistoryManager.Remove(id);
                this.Frame.Navigate(typeof(HistoryPage));
                ShowMsg("履歴を削除しました。");
            }
        }
    }
}
