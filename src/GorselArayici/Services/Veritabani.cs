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

        // WAL: tarama yazarken arama aynı anda okuyabilsin.
        Calistir("PRAGMA journal_mode = WAL");

        // degisme: dosyanın son değişme anı. Dosya değişirse yeniden okunur.
        Calistir("""
            CREATE TABLE IF NOT EXISTS resimler (
                yol     TEXT PRIMARY KEY,
                degisme INTEGER NOT NULL,
                metin   TEXT NOT NULL
            )
            """);

        // Arama dizini (kitabın arkasındaki dizin gibi). Satır numarası resimler tablosuyla aynıdır.
        // İçinde normalleştirilmiş metin durur: "Şirket" → "sirket".
        Calistir("CREATE VIRTUAL TABLE IF NOT EXISTS ara USING fts5(metin)");
    }

    /// <summary>
    /// Açılışta bir kez: veritabanı sağlam mı? Bozuksa silinmez, yanına "galeri.bozuk-…db" adıyla kaldırılır ve
    /// temiz bir veritabanıyla baştan başlanır. Veri resimlerden üretildiği için hiçbir şey kaybolmaz, yalnız
    /// tarama bir kez baştan yapılır (~7 dk). Taramadan önce çağrılır.
    /// </summary>
    /// <returns>Bozuk çıkıp yenisi kurulduysa true.</returns>
    public static bool SaglikKontrolu()
    {
        if (!File.Exists(DosyaYolu))
            return false;

        bool saglam;
        try
        {
            using var vt = new Veritabani();
            // quick_check: tablolar ve indeksler tutarlı mı. FTS5'in kendi dizinini ayrıca kendisine sorarız;
            // bozuksa bu komut hata fırlatır.
            saglam = vt.Tek("PRAGMA quick_check") == "ok";
            if (saglam)
                vt.Calistir("INSERT INTO ara(ara) VALUES('integrity-check')");
        }
        catch (SqliteException)
        {
            saglam = false;
        }
        if (saglam)
            return false;

        // Açık bağlantılar dosyayı tutmasın, sonra ana dosyayı ve yan dosyalarını (-wal, -shm) birlikte kenara al.
        SqliteConnection.ClearAllPools();
        var ek = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        try
        {
            foreach (var uzanti in new[] { "", "-wal", "-shm" })
            {
                var kaynak = DosyaYolu + uzanti;
                if (File.Exists(kaynak))
                    File.Move(kaynak, Path.Combine(Path.GetDirectoryName(DosyaYolu)!, $"galeri.bozuk-{ek}.db{uzanti}"));
            }
            return true;
        }
        catch (IOException)
        {
            return false; // dosya o an kilitliyse eskisiyle devam; bir sonraki açılışta tekrar denenir
        }
    }

    /// <summary>
    /// Dizin boşsa (dizinden önce okunmuş eski kayıtlar) resimler tablosundan bir kerede doldurur.
    /// </summary>
    public void DiziniHazirla()
    {
        using var say = _baglanti.CreateCommand();
        say.CommandText = "SELECT (SELECT count(*) FROM ara), (SELECT count(*) FROM resimler)";
        using (var okuyucu = say.ExecuteReader())
        {
            okuyucu.Read();
            if (okuyucu.GetInt64(0) == okuyucu.GetInt64(1))
                return;
        }

        var kayitlar = new List<(long Id, string Metin)>();
        using (var oku = _baglanti.CreateCommand())
        {
            oku.CommandText = "SELECT rowid, metin FROM resimler";
            using var okuyucu = oku.ExecuteReader();
            while (okuyucu.Read())
                kayitlar.Add((okuyucu.GetInt64(0), okuyucu.GetString(1)));
        }

        using var islem = _baglanti.BeginTransaction();
        Calistir("DELETE FROM ara");
        using var ekle = _baglanti.CreateCommand();
        ekle.CommandText = "INSERT INTO ara (rowid, metin) VALUES ($id, $metin)";
        var id = ekle.Parameters.Add("$id", SqliteType.Integer);
        var metin = ekle.Parameters.Add("$metin", SqliteType.Text);
        foreach (var kayit in kayitlar)
        {
            id.Value = kayit.Id;
            metin.Value = Normallestirici.Yap(kayit.Metin);
            ekle.ExecuteNonQuery();
        }
        islem.Commit();
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
            ON CONFLICT(yol) DO UPDATE SET degisme = excluded.degisme, metin = excluded.metin;

            DELETE FROM ara WHERE rowid = (SELECT rowid FROM resimler WHERE yol = $yol);
            INSERT INTO ara (rowid, metin) VALUES ((SELECT rowid FROM resimler WHERE yol = $yol), $normal);
            """;
        var yol = komut.Parameters.Add("$yol", SqliteType.Text);
        var degisme = komut.Parameters.Add("$degisme", SqliteType.Integer);
        var metin = komut.Parameters.Add("$metin", SqliteType.Text);
        var normal = komut.Parameters.Add("$normal", SqliteType.Text);

        foreach (var kayit in kayitlar)
        {
            yol.Value = kayit.Yol;
            degisme.Value = kayit.Degisme;
            metin.Value = kayit.Metin;
            normal.Value = Normallestirici.Yap(kayit.Metin);
            komut.ExecuteNonQuery();
        }
        islem.Commit();
    }

    /// <summary>
    /// Yazdığın her kelimeyle başlayan kelimeleri içeren resimleri bulur ("fat" → "fatura").
    /// Birden çok kelime yazılırsa hepsi geçmeli. En alakalı sonuç önce gelir.
    /// Hepsi döner (sınırsız): tarihe göre sıralarken en yenileri kaçırmamak için.
    /// </summary>
    public List<(string Yol, string Metin, long Degisme)> Ara(string sorgu)
    {
        var kelimeler = Normallestirici.Kelimeler(sorgu);
        if (kelimeler.Length == 0)
            return [];

        // Her kelime tırnak içinde: kullanıcının yazdığı özel karakterler arama komutu sanılmasın.
        var fts = string.Join(" ", kelimeler.Select(k => $"\"{k}\"*"));

        using var komut = _baglanti.CreateCommand();
        komut.CommandText = """
            SELECT r.yol, r.metin, r.degisme
            FROM ara JOIN resimler r ON r.rowid = ara.rowid
            WHERE ara MATCH $sorgu
            ORDER BY ara.rank
            """;
        komut.Parameters.AddWithValue("$sorgu", fts);

        var sonuc = new List<(string, string, long)>();
        using var okuyucu = komut.ExecuteReader();
        while (okuyucu.Read())
            sonuc.Add((okuyucu.GetString(0), okuyucu.GetString(1), okuyucu.GetInt64(2)));
        return sonuc;
    }

    private string? Tek(string sql)
    {
        using var komut = _baglanti.CreateCommand();
        komut.CommandText = sql;
        return komut.ExecuteScalar()?.ToString();
    }

    private void Calistir(string sql)
    {
        using var komut = _baglanti.CreateCommand();
        komut.CommandText = sql;
        komut.ExecuteNonQuery();
    }

    public void Dispose() => _baglanti.Dispose();
}
