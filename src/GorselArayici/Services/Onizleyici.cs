using System.IO;
using System.Windows.Media.Imaging;

namespace GorselArayici.Services;

/// <summary>
/// Kutularda gösterilecek küçük önizleme resmini hazırlar.
/// </summary>
public static class Onizleyici
{
    // Kutudaki resim alanının yüksekliği kadar çözülür: 2500 piksellik ekran görüntüsünü
    // tam boy bellekte tutmak yerine küçük hali tutulur (resim başı ~100 KB yerine MB'lar).
    private const int Yukseklik = 240;

    public static BitmapImage? Yukle(string yol)
    {
        try
        {
            using var dosya = File.OpenRead(yol);
            var resim = new BitmapImage();
            resim.BeginInit();
            resim.StreamSource = dosya;
            resim.DecodePixelHeight = Yukseklik;
            resim.CacheOption = BitmapCacheOption.OnLoad; // dosya hemen okunur, sonra kapatılabilir
            resim.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            resim.EndInit();
            resim.Freeze(); // başka iş parçacığında hazırlanan resmi pencere kullanabilsin
            return resim;
        }
        catch
        {
            return null; // açılamayan resmin kutusu boş kalır
        }
    }
}
