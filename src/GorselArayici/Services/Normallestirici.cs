using System.Text.RegularExpressions;

namespace GorselArayici.Services;

/// <summary>
/// Aramada harf farklarını yok sayar: "ŞİRKET", "sirket", "şirket" aynı kelimedir.
/// Türkçe OCR İngilizce metindeki i'yi "İ" okuyabildiği için i/ı/İ/I hepsi "i" sayılır.
/// Her harf tek bir harfe dönüşür; metnin uzunluğu değişmez (özet çıkarırken konumlar tutsun diye).
/// </summary>
public static class Normallestirici
{
    public static string Yap(string metin)
    {
        var harfler = metin.ToCharArray();
        for (var i = 0; i < harfler.Length; i++)
        {
            harfler[i] = harfler[i] switch
            {
                'ı' or 'İ' or 'I' or 'i' => 'i',
                'ş' or 'Ş' => 's',
                'ç' or 'Ç' => 'c',
                'ğ' or 'Ğ' => 'g',
                'ö' or 'Ö' => 'o',
                'ü' or 'Ü' => 'u',
                var h => char.ToLowerInvariant(h),
            };
        }
        return new string(harfler);
    }

    /// <summary>Metni kelimelere böler (harf ve rakam dışındaki her şey ayırıcıdır).</summary>
    public static string[] Kelimeler(string metin) =>
        Regex.Split(Yap(metin), @"[^\p{L}\p{N}]+")
            .Where(k => k.Length > 0)
            .ToArray();
}
