using System.IO;
using System.Text.Json;

namespace GorselArayici.Services;

/// <summary>
/// Kullanıcının seçtiği küçük tercihleri (ızgaradaki sütun sayısı, tema) hatırlar.
/// Veritabanının yanında, AppData\Local\GorselArayici\ayarlar.json dosyasında durur.
/// </summary>
public sealed class Ayarlar
{
    public int SutunSayisi { get; set; } = 6;

    /// <summary>null: Windows'un ayarına uy. "Koyu" ya da "Acik": kullanıcı düğmeyle seçti.</summary>
    public string? Tema { get; set; }

    /// <summary>
    /// Uygulamanın tek ayar nesnesi. Herkes bunu kullanır: iki ayrı kopya olsaydı biri kaydederken
    /// öbürünün değişikliğini eski değerle ezerdi (sütun sayısını kaydeden, temayı geri alırdı).
    /// </summary>
    public static Ayarlar Simdiki { get; } = Oku();

    private static string DosyaYolu =>
        Path.Combine(Path.GetDirectoryName(Veritabani.DosyaYolu)!, "ayarlar.json");

    private static Ayarlar Oku()
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
