using System.IO;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace GorselArayici.Services;

/// <summary>Resimdeki bir kelime ve resmin üzerindeki yeri (piksel olarak).</summary>
public record KelimeKonumu(string Metin, double X, double Y, double Genislik, double Yukseklik);

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

    /// <summary>Taramada kullanılır: resimdeki bütün yazı, tek metin olarak.</summary>
    public async Task<string> OkuAsync(string yol)
    {
        var (sonuc, _) = await TaniAsync(yol, ExifOrientationMode.RespectExifOrientation);
        return sonuc.Text;
    }

    /// <summary>
    /// Büyük görünümde kullanılır: her kelime ve resmin üzerindeki yeri.
    /// Konumlar orijinal resmin pikselleriyle verilir (küçültüldüyse geri büyütülür).
    /// </summary>
    public async Task<List<KelimeKonumu>> KelimeleriBulAsync(string yol)
    {
        // Ekrandaki resim döndürülmeden gösterildiği için burada da döndürmüyoruz; yoksa kutular kayar.
        var (sonuc, oran) = await TaniAsync(yol, ExifOrientationMode.IgnoreExifOrientation);
        return sonuc.Lines
            .SelectMany(satir => satir.Words)
            .Select(k => new KelimeKonumu(k.Text,
                k.BoundingRect.X / oran, k.BoundingRect.Y / oran,
                k.BoundingRect.Width / oran, k.BoundingRect.Height / oran))
            .ToList();
    }

    /// <returns>OCR sonucu ve resmin ne kadar küçültüldüğü (1 = hiç).</returns>
    private async Task<(OcrResult Sonuc, double Oran)> TaniAsync(string yol, ExifOrientationMode yon)
    {
        using var dosya = File.OpenRead(yol);
        var cozucu = await BitmapDecoder.CreateAsync(dosya.AsRandomAccessStream());

        // OCR motorunun kabul ettiği en büyük boyuttan büyükse küçültülür.
        var donusum = new BitmapTransform();
        var oran = 1.0;
        var kenar = Math.Max(cozucu.PixelWidth, cozucu.PixelHeight);
        if (kenar > OcrEngine.MaxImageDimension)
        {
            oran = (double)OcrEngine.MaxImageDimension / kenar;
            donusum.ScaledWidth = (uint)(cozucu.PixelWidth * oran);
            donusum.ScaledHeight = (uint)(cozucu.PixelHeight * oran);
        }

        using var resim = await cozucu.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, donusum,
            yon, ColorManagementMode.DoNotColorManage);

        return (await _motor.RecognizeAsync(resim), oran);
    }
}
