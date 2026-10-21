using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GorselArayici.Services;
using GorselArayici.ViewModels;

namespace GorselArayici;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private bool _kapatilabilir;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.YukleAsync();

        // Çift tık: kutunun kendisi tıklamayı yuttuğu için XAML'deki MouseBinding'e ulaşmıyordu.
        // Burada tıklanan yerin hangi kutuya ait olduğunu bulup o resmi büyük açıyoruz.
        SonucListesi.MouseDoubleClick += (_, e) =>
        {
            if (TiklananKutu(e.OriginalSource) is { } oge)
                _viewModel.GoruntuleCommand.Execute(oge);
        };

        // Ctrl + tekerlek: yukarı = daha az ve büyük kare, aşağı = daha çok ve küçük kare (iPhone gibi).
        // Ctrl'siz tekerlek her zamanki gibi kaydırır.
        SonucListesi.PreviewMouseWheel += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
                return;
            _viewModel.SutunSayisiniDegistir(e.Delta > 0 ? -1 : +1);
            e.Handled = true;
        };

        YakinlastirmayiBagla();

        _viewModel.Goruntuleyici.PropertyChanged += (_, e) =>
        {
            // Başka resme geçince ya da büyük görünüm açılıp kapanınca yeni resim pencereye sığmış olarak başlasın.
            if (e.PropertyName is nameof(GoruntuleyiciViewModel.Secili) or nameof(GoruntuleyiciViewModel.Acik))
                Sigdir();

            // Büyük görünüm kapanınca, en son bakılan resmin kutusu seçili ve görünür olsun.
            if (e.PropertyName == nameof(GoruntuleyiciViewModel.Acik) && !_viewModel.Goruntuleyici.Acik)
            {
                if (_viewModel.Goruntuleyici.Secili is not { } secili)
                    return;
                SonucListesi.SelectedItem = secili;
                SonucListesi.ScrollIntoView(secili);
                (SonucListesi.ItemContainerGenerator.ContainerFromItem(secili) as UIElement)?.Focus();
            }
        };

        PreviewKeyDown += TusaBasildi;

        // Tema düğmesi ve başlık çubuğu
        TemaDugmesi.Click += (_, _) => Tema.Degistir();
        Tema.Degisti += TemaSimgesiniGuncelle;
        TemaSimgesiniGuncelle();
        SourceInitialized += (_, _) => Tema.BaslikCubugunuBoya(this);

        // Ctrl+Alt+F: pencere tutamacı (handle) ancak SourceInitialized'da hazır olduğu için burada.
        KisayolTusu? kisayol = null;
        SourceInitialized += (_, _) => kisayol = new KisayolTusu(this, OneGetir);
        Closed += (_, _) => kisayol?.Dispose();

        // Kapanırken önce taramayı durdurup son grubun kaydını bekle, sonra kapan.
        // Pencere hemen gizlenir; kullanıcı beklemez. Sonraki açılışta kaldığı yerden devam eder.
        // Pencerenin dışındaki silik alana tıklamak da galeriye döndürür.
        GoruntuleyiciArkasi.MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource == GoruntuleyiciArkasi)
                _viewModel.Goruntuleyici.KapatCommand.Execute(null);
        };

        Closing += async (_, e) =>
        {
            // Büyük görünüm açıkken uygulamanın çarpısı uygulamayı kapatmaz, sadece galeriye döndürür.
            if (_viewModel.Goruntuleyici.Acik)
            {
                e.Cancel = true;
                _viewModel.Goruntuleyici.KapatCommand.Execute(null);
                return;
            }
            if (_kapatilabilir)
                return;
            e.Cancel = true;
            Hide();
            await _viewModel.DurdurAsync();
            _kapatilabilir = true;
            Close();
        };
    }

    private void TusaBasildi(object sender, KeyEventArgs e)
    {
        var goruntuleyici = _viewModel.Goruntuleyici;
        var ctrlC = e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control;

        if (goruntuleyici.Acik)
        {
            switch (e.Key)
            {
                case Key.Left: goruntuleyici.OncekiCommand.Execute(null); break;
                case Key.Right: goruntuleyici.SonrakiCommand.Execute(null); break;
                case Key.Escape: goruntuleyici.KapatCommand.Execute(null); break;
                case Key.C when ctrlC: _viewModel.Kopyala(goruntuleyici.Secili); break;
                default: return;
            }
            e.Handled = true;
            return;
        }

        // Arama kutusunda yazı seçiliyse Ctrl+C her zamanki gibi o yazıyı kopyalasın.
        if (ctrlC && !(AramaKutusu.IsKeyboardFocused && AramaKutusu.SelectionLength > 0))
        {
            _viewModel.Kopyala(SonucListesi.SelectedItem as ResimOgesi);
            e.Handled = true;
        }
    }

    // ===== Büyük görünümde yakınlaştırma =====
    // Resim (ve üstündeki işaretler) iki dönüşümle gösterilir: önce Olcek ile büyütülür, sonra Kaydirma ile
    // yerinden oynatılır. Ekrandaki nokta = olcek × resimdeki nokta + kaydirma.
    private const double EnCokYakinlik = 8;   // en fazla 8 kat
    private const double AdimCarpani = 1.2;   // tekerleğin bir "tık"ı %20 büyütür
    private bool _surukleniyor;
    private Point _surukleBaslangici;
    private Point _kaydirmaBaslangici;

    private void YakinlastirmayiBagla()
    {
        ResimKabi.PreviewMouseWheel += (_, e) =>
        {
            var eski = Olcek.ScaleX;
            var yeni = Math.Clamp(eski * Math.Pow(AdimCarpani, e.Delta / 120.0), 1, EnCokYakinlik);
            // Görünen alanın ortası yerinde kalsın: yakınlaştırma hep ortaya doğru olur (kaydırdıysan o anki ortaya).
            // Orta nokta resmin KENDİ koordinatına çevrilir (dönüşümden önceki hali); dış alana göre hesaplanırsa
            // resim alanın içinde ortalı durduğu için aradaki boşluk kadar kayar.
            // Ekrandaki yer = olcek × nokta + kaydirma; olcek değişince aynı yerde kalması için kaydirma += nokta × (eski − yeni).
            var orta = new Point(ResimKabi.ActualWidth / 2, ResimKabi.ActualHeight / 2);
            var nokta = ResimKabi.TranslatePoint(orta, ResimAlani);
            Kaydirma.X += nokta.X * (eski - yeni);
            Kaydirma.Y += nokta.Y * (eski - yeni);
            Olcek.ScaleX = Olcek.ScaleY = yeni;
            KaydirmayiSinirla();
            e.Handled = true;
        };

        ResimKabi.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                Sigdir();
                return;
            }
            if (Olcek.ScaleX <= 1)
                return; // sığmış resimde kaydıracak bir şey yok
            _surukleniyor = true;
            _surukleBaslangici = e.GetPosition(ResimKabi);
            _kaydirmaBaslangici = new Point(Kaydirma.X, Kaydirma.Y);
            ResimKabi.CaptureMouse(); // fare alanın dışına çıksa da sürükleme devam etsin
        };

        ResimKabi.MouseMove += (_, e) =>
        {
            if (!_surukleniyor)
                return;
            var simdi = e.GetPosition(ResimKabi);
            Kaydirma.X = _kaydirmaBaslangici.X + (simdi.X - _surukleBaslangici.X);
            Kaydirma.Y = _kaydirmaBaslangici.Y + (simdi.Y - _surukleBaslangici.Y);
            KaydirmayiSinirla();
        };

        ResimKabi.MouseLeftButtonUp += (_, _) =>
        {
            _surukleniyor = false;
            ResimKabi.ReleaseMouseCapture();
        };
    }

    /// <summary>Resim kaçmasın: büyütülmüş resim, sığmış halinin kapladığı kutuyu her zaman doldursun.</summary>
    private void KaydirmayiSinirla()
    {
        var olcek = Olcek.ScaleX;
        Kaydirma.X = Math.Clamp(Kaydirma.X, ResimAlani.ActualWidth * (1 - olcek), 0);
        Kaydirma.Y = Math.Clamp(Kaydirma.Y, ResimAlani.ActualHeight * (1 - olcek), 0);
        ResimKabi.Cursor = olcek > 1 ? Cursors.SizeAll : null;
    }

    /// <summary>Yakınlaştırmayı sıfırla: resim yeniden pencereye sığar.</summary>
    private void Sigdir()
    {
        Olcek.ScaleX = Olcek.ScaleY = 1;
        Kaydirma.X = Kaydirma.Y = 0;
        _surukleniyor = false;
        ResimKabi.Cursor = null;
    }

    private void TemaSimgesiniGuncelle()
    {
        TemaSimgesi.Text = Tema.Koyu ? "" : ""; // güneş : ay (Windows simge fontu)
        TemaDugmesi.ToolTip = Tema.Koyu ? "Açık temaya geç" : "Koyu temaya geç";
    }

    /// <summary>Kısayol tuşuna basılınca: pencere öne gelir, arama kutusu yazmaya hazır olur.</summary>
    private void OneGetir()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        _viewModel.Goruntuleyici.KapatCommand.Execute(null);
        AramaKutusu.Focus();
        AramaKutusu.SelectAll(); // eski aramanın üstüne direkt yazılabilsin
    }

    private ResimOgesi? TiklananKutu(object kaynak) =>
        kaynak is DependencyObject tiklanan
        && ItemsControl.ContainerFromElement(SonucListesi, tiklanan) is ListBoxItem { DataContext: ResimOgesi oge }
            ? oge
            : null;
}
