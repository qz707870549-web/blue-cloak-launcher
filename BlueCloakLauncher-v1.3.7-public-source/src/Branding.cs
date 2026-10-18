using System.Windows.Media.Imaging;

namespace Luma;

public static class Branding
{
    public const string Name = "蓝斗篷启动";
    public static BitmapSource Avatar()
    {
        using (var stream = typeof(Branding).Assembly.GetManifestResourceStream("Luma.Avatar"))
        {
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 256;
            image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
        }
    }
}
