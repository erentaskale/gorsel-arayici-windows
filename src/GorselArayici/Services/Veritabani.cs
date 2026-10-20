using System.IO;
using Microsoft.Data.Sqlite;

namespace GorselArayici.Services;

/// <summary>
/// Okunan yazıları saklar. Dosya repo klasöründe değil, AppData\Local\GorselArayici altında durur:
/// kişisel veri koddan ayrı kalır.
/// </summary>
public sealed class Veritabani : IDisposable
{
    public static string DosyaYolu => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GorselArayici", "galeri.db");

    private readonly SqliteConnection _baglanti;

    public Veritabani()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DosyaYolu)!);
        _baglanti = new SqliteConnection($"Data Source={DosyaYolu}");
        _baglanti.Open();

        // degisme: dosyanın son değişme anı. Dosya değişirse yeniden okunur.
        Calistir("""
            CREATE TABLE IF NOT EXISTS resimler (
                yol     TEXT PRIMARY KEY,
                degisme INTEGER NOT NULL,
                metin   TEXT NOT NULL
            )
            """);
    }

    /// <summary>Daha önce okunmuş resimler ve okunduklarındaki değişme anları.</summary>
    public Dictionary<string, long> OkunanlariGetir()
    {
        var sonuc = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        using var komut = _baglanti.CreateCommand();
        komut.CommandText = "SELECT yol, degisme FROM resimler";
        using var okuyucu = komut.ExecuteReader();
        while (okuyucu.Read())
            sonuc[okuyucu.GetString(0)] = okuyucu.GetInt64(1);
        return sonuc;
    }

    /// <summary>Bir grup sonucu tek seferde yazar (tek tek yazmaktan çok daha hızlı).</summary>
    public void Kaydet(IEnumerable<(string Yol, long Degisme, string Metin)> kayitlar)
    {
        using var islem = _baglanti.BeginTransaction();
        using var komut = _baglanti.CreateCommand();
        komut.CommandText = """
            INSERT INTO resimler (yol, degisme, metin) VALUES ($yol, $degisme, $metin)
            ON CONFLICT(yol) DO UPDATE SET degisme = excluded.degisme, metin = excluded.metin
            """;
        var yol = komut.Parameters.Add("$yol", SqliteType.Text);
        var degisme = komut.Parameters.Add("$degisme", SqliteType.Integer);
        var metin = komut.Parameters.Add("$metin", SqliteType.Text);

        foreach (var kayit in kayitlar)
        {
            yol.Value = kayit.Yol;
            degisme.Value = kayit.Degisme;
            metin.Value = kayit.Metin;
            komut.ExecuteNonQuery();
        }
        islem.Commit();
    }

    private void Calistir(string sql)
    {
        using var komut = _baglanti.CreateCommand();
        komut.CommandText = sql;
        komut.ExecuteNonQuery();
    }

    public void Dispose() => _baglanti.Dispose();
}
