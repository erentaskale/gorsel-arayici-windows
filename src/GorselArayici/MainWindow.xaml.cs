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

        // Ctrl + tekerlek: kutuları büyüt/küçült. Ctrl'siz tekerlek her zamanki gibi kaydırır.
        SonucListesi.PreviewMouseWheel += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
                return;
            _viewModel.KutuBoyutunuDegistir(e.Delta > 0 ? +1 : -1);
            e.Handled = true;
        };

        // Büyük görünüm kapanınca, en son bakılan resmin kutusu seçili ve görünür olsun.
        _viewModel.Goruntuleyici.PropertyChanged += (_, e) =>
        {
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
