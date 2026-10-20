using System.IO;
using System.Windows.Media.Imaging;

namespace GorselArayici.Services;

/// <summary>
/// Kutularda ve büyük görünümde gösterilecek resimleri hazırlar.
/// </summary>
public static class Onizleyici
{
    // Izgaradaki kare, resmin ortasını kırparak gösterir (iPhone gibi); bu yüzden resmin kısa kenarı
    // karenin en büyük boyutu kadar olsun yeter. 2500 piksellik ekran görüntüsünü tam boy bellekte
    // tutmak yerine bu küçük hali tutulur.
    private const int KareEnBuyuk = 320;

    /// <summary>
    /// Kutu için küçük önizleme ve resmin gerçek piksel boyutu. Açılamazsa resim null (kutu boş kalır).
    /// </summary>
    public static (BitmapImage? Resim, int Genislik, int Yukseklik) KutuIcinYukle(string yol)
    {
        try
        {
            using var dosya = File.OpenRead(yol);

            // Önce yalnız boyutu oku (resmin tamamını çözmeden), sonra kısa kenarı kareyi dolduracak ölçüde çöz.
            var kare = BitmapDecoder.Create(dosya, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.None).Frames[0];
            var oran = Math.Min(1.0, (double)KareEnBuyuk / Math.Min(kare.PixelWidth, kare.PixelHeight));
            dosya.Position = 0;

            return (Coz(dosya, (int)Math.Max(1, kare.PixelWidth * oran)), kare.PixelWidth, kare.PixelHeight);
        }
        catch
        {
            return (null, 0, 0);
        }
    }

    /// <summary>Büyük görünüm için resmin tam hali.</summary>
    public static BitmapImage? TamYukle(string yol)
    {
        try
        {
            using var dosya = File.OpenRead(yol);
            return Coz(dosya, genislik: null);
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage Coz(Stream dosya, int? genislik)
    {
        var resim = new BitmapImage();
        resim.BeginInit();
        resim.StreamSource = dosya;
        if (genislik is int g)
            resim.DecodePixelWidth = g;
        resim.CacheOption = BitmapCacheOption.OnLoad; // dosya hemen okunur, sonra kapatılabilir
        resim.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        resim.EndInit();
        resim.Freeze(); // başka iş parçacığında hazırlanan resmi pencere kullanabilsin
        return resim;
    }
}
