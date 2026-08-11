using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace GrassiBoard.Services.Remote;

internal static class RemoteQrCodeService
{
    public static BitmapImage? Create(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        using var generator = new QRCodeGenerator();
        using QRCodeData data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var code = new PngByteQRCode(data);
        byte[] png = code.GetGraphic(8, drawQuietZones: true);
        using var stream = new MemoryStream(png, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
