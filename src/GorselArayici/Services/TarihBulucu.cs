using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace GorselArayici.Services;

/// <summary>
/// Resmin ne zaman çekildiğini bulur. Ekran görüntülerinde EXIF tarihi olmaz ama Windows tarihi
/// dosya adına yazar ("Ekran görüntüsü 2024-01-31 143000"). Önce ad, bulunamazsa dosyanın tarihi.
/// </summary>
public static partial class TarihBulucu
{
    // Ekran görüntüsü: 2024-01-31 143000
    [GeneratedRegex(@"(\d{4})-(\d{2})-(\d{2}) (\d{2})(\d{2})(\d{2})")]
    private static partial Regex EkranGoruntusu();

    // Kamera uygulaması: WIN_20240516_16_49_00
    [GeneratedRegex(@"(\d{4})(\d{2})(\d{2})_(\d{2})_(\d{2})_(\d{2})")]
    private static partial Regex Kamera();

    public static DateTime Bul(string yol, long degismeTicks)
    {
        var ad = Path.GetFileNameWithoutExtension(yol);
        foreach (var kalip in new[] { EkranGoruntusu(), Kamera() })
        {
            var e = kalip.Match(ad);
            if (e.Success && DateTime.TryParseExact(
                    string.Concat(e.Groups.Values.Skip(1).Select(g => g.Value)),
                    "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tarih))
                return tarih;
        }

        // Adında tarih yoksa: dosyanın son değişme anı (veritabanında zaten kayıtlı).
        return new DateTime(degismeTicks, DateTimeKind.Utc).ToLocalTime();
    }
}
