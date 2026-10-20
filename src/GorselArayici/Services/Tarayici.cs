using System.IO;

namespace GorselArayici.Services;

public record TaramaDurumu(int Biten, int Toplam, string SonDosya, string SonMetin);

/// <summary>
/// Henüz okunmamış (ya da sonradan değişmiş) resimleri okur, sonuçları veritabanına yazar.
/// Her 100 resimde bir kaydeder: pencere kapansa bile kaldığı yerden devam eder.
/// </summary>
public static class Tarayici
{
    private const int GrupBoyutu = 100;

    /// <returns>Yeni okunan resim sayısı.</returns>
    public static async Task<int> TaraAsync(
        IReadOnlyList<string> yollar, IProgress<TaramaDurumu> ilerleme, CancellationToken iptal)
    {
        using var veritabani = new Veritabani();
        var okunanlar = veritabani.OkunanlariGetir();

        // Yalnız yeni ya da değişmiş dosyalar okunur.
        var okunacaklar = yollar
            .Select(yol => (Yol: yol, Degisme: File.GetLastWriteTimeUtc(yol).Ticks))
            .Where(d => !okunanlar.TryGetValue(d.Yol, out var eski) || eski != d.Degisme)
            .ToList();

        if (okunacaklar.Count == 0)
            return 0;

        var okuyucu = new OcrOkuyucu();
        var grup = new List<(string, long, string)>(GrupBoyutu);
        var biten = 0;

        foreach (var (yol, degisme) in okunacaklar)
        {
            if (iptal.IsCancellationRequested)
                break;

            string metin;
            try
            {
                metin = await okuyucu.OkuAsync(yol);
            }
            catch
            {
                // Okunamayan resim (bozuk dosya, desteklenmeyen tür) boş metinle kaydedilir,
                // her açılışta tekrar denenmez. Dosya değişirse yeniden denenir.
                metin = "";
            }

            grup.Add((yol, degisme, metin));
            biten++;

            if (grup.Count == GrupBoyutu)
            {
                veritabani.Kaydet(grup);
                grup.Clear();
            }

            ilerleme.Report(new TaramaDurumu(biten, okunacaklar.Count, Path.GetFileName(yol), metin));
        }

        veritabani.Kaydet(grup);
        return biten;
    }
}
