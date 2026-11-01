using System;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using AutoBackup.Services;
using AutoBackup.UI;

namespace AutoBackup;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        Ui.InstallGlobalResources(app);

        app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            try
            {
                MessageBox.Show("Auto Backup hit an unexpected problem.\n\n" + e.Exception.Message,
                    AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
            }
        };

        try
        {
            var load = ConfigStore.Load();
            var shell = new ShellWindow(load);
            app.MainWindow = shell;
            app.Run(shell);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Auto Backup could not start.\n\n" + ex.Message,
                AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
