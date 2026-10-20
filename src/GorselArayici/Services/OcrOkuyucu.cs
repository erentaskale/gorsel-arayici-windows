using System.IO;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace GorselArayici.Services;

/// <summary>
/// Windows'un kendi OCR motoruyla resimdeki yazıyı okur. İnternet gerekmez.
/// </summary>
public sealed class OcrOkuyucu
{
    private readonly OcrEngine _motor;

    public OcrOkuyucu()
    {
        // Windows'taki dil ayarına göre (sende Türkçe) motor seçilir.
        _motor = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException(
                "Windows'ta OCR dil paketi bulunamadı. Ayarlar > Dil'den Türkçe paketini ekleyin.");
    }

    public async Task<string> OkuAsync(string yol)
    {
        using var dosya = File.OpenRead(yol);
        var cozucu = await BitmapDecoder.CreateAsync(dosya.AsRandomAccessStream());

        // OCR motorunun kabul ettiği en büyük boyuttan büyükse küçültülür.
        var donusum = new BitmapTransform();
        var enBuyuk = OcrEngine.MaxImageDimension;
        var kenar = Math.Max(cozucu.PixelWidth, cozucu.PixelHeight);
        if (kenar > enBuyuk)
        {
            var oran = (double)enBuyuk / kenar;
            donusum.ScaledWidth = (uint)(cozucu.PixelWidth * oran);
            donusum.ScaledHeight = (uint)(cozucu.PixelHeight * oran);
        }

        using var resim = await cozucu.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, donusum,
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

        var sonuc = await _motor.RecognizeAsync(resim);
        return sonuc.Text;
    }
}
