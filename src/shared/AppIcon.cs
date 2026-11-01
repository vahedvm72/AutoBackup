using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AutoBackup.UI;

public static class AppIcon
{
    private static ImageSource _cache;

    public static ImageSource Get()
    {
        if (_cache != null)
        {
            return _cache;
        }
        try
        {
            var exe = Process.GetCurrentProcess().MainModule.FileName;
            using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(exe))
            {
                if (icon != null)
                {
                    var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    _cache = source;
                }
            }
        }
        catch
        {
        }
        return _cache;
    }
}
