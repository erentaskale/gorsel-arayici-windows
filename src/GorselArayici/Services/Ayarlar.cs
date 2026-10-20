using System.IO;
using System.Text.Json;

namespace GorselArayici.Services;

/// <summary>
/// Kullanıcının seçtiği küçük tercihleri (şimdilik kutu boyutu) hatırlar.
/// Veritabanının yanında, AppData\Local\GorselArayici\ayarlar.json dosyasında durur.
/// </summary>
public sealed class Ayarlar
{
    public double KutuGenisligi { get; set; } = 190;

    private static string DosyaYolu =>
        Path.Combine(Path.GetDirectoryName(Veritabani.DosyaYolu)!, "ayarlar.json");

    public static Ayarlar Oku()
    {
        try
        {
            return JsonSerializer.Deserialize<Ayarlar>(File.ReadAllText(DosyaYolu)) ?? new Ayarlar();
        }
        catch
        {
            return new Ayarlar(); // ilk açılış ya da bozuk dosya: varsayılanlar
        }
    }

    public void Kaydet()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DosyaYolu)!);
            File.WriteAllText(DosyaYolu, JsonSerializer.Serialize(this));
        }
        catch
        {
            // Tercih kaydedilemezse uygulama çalışmaya devam eder; sonraki açılışta varsayılan gelir.
        }
    }
}
