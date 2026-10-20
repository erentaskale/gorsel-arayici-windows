using System.IO;

namespace GorselArayici.Services;

/// <summary>
/// Windows Fotoğraflar'ın Galeri'sindeki resimleri bulur.
/// Galeri, kullanıcının Resimler klasöründen beslenir (OneDrive yedeklemesi açıksa OneDrive altındadır).
/// </summary>
public static class GaleriBulucu
{
    // OCR'ın okuyabildiği resim türleri. Videolar şimdilik dışarıda.
    private static readonly HashSet<string> Uzantilar = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".heic", ".webp", ".bmp", ".gif", ".tif", ".tiff"
    };

    public static string GaleriKlasoru =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    public static List<string> ResimleriBul()
    {
        var secenekler = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true, // izin verilmeyen klasörde çökmesin, atlasın
        };

        return Directory.EnumerateFiles(GaleriKlasoru, "*", secenekler)
            .Where(yol => Uzantilar.Contains(Path.GetExtension(yol)))
            .ToList();
    }
}
