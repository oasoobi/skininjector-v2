using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using System.Diagnostics;
using WinRT;


namespace skininjector_v2
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {

        private DesktopAcrylicController acrylicController;

        private SystemBackdropConfiguration backdropConfiguration;



        public MainWindow()
        {
            this.InitializeComponent();

            RootFrame.Navigate(typeof(HomePage));

            Debug.WriteLine("起動した。");

            WindowHelper.SetMinSize(this, 1000, 700);
            TrySetAcrylicBackdrop();

            
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

        

        

        
    }

    
}
