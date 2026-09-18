using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Infinity8;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<SavedPhoto> _savedPhotos = [];
    private readonly DispatcherTimer _liveTimer;
    private int _frameSeed;
    private int _exposure = 50;
    private bool _isConnected;
    private bool _isShowingSavedPhoto;
    private BitmapSource? _lastLiveFrame;

    public MainWindow()
    {
        InitializeComponent();
        GalleryList.ItemsSource = _savedPhotos;
        _liveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _liveTimer.Tick += (_, _) => RenderLiveFrame();
        UpdateStatus();
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isConnected)
        {
            Disconnect();
            return;
        }

        _isConnected = true;
        _isShowingSavedPhoto = false;
        _liveTimer.Start();
        RenderLiveFrame();
        UpdateStatus();
    }

    private void Disconnect()
    {
        _isConnected = false;
        _isShowingSavedPhoto = false;
        _liveTimer.Stop();
        GalleryList.SelectedItem = null;
        UpdateStatus();
    }

    private void ExposureSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _exposure = (int)e.NewValue;
        ExposureValueText.Text = _exposure.ToString();
        if (_isConnected && !_isShowingSavedPhoto)
        {
            RenderLiveFrame();
        }
    }

    private void SavePhotoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastLiveFrame is null || !_isConnected)
        {
            return;
        }

        var captured = new SavedPhoto(
            $"Photo {_savedPhotos.Count + 1} ({DateTime.Now:HH:mm:ss})",
            _lastLiveFrame);
        _savedPhotos.Insert(0, captured);
    }

    private void GalleryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isConnected)
        {
            GalleryList.SelectedItem = null;
            return;
        }

        if (GalleryList.SelectedItem is not SavedPhoto selected)
        {
            return;
        }

        _isShowingSavedPhoto = true;
        MainImage.Source = selected.FullImage;
        OverlayStatusText.Text = $"Peržiūra: {selected.Name}";
        ReturnToLiveButton.IsEnabled = _isConnected;
    }

    private void ReturnToLiveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isConnected)
        {
            return;
        }

        _isShowingSavedPhoto = false;
        GalleryList.SelectedItem = null;
        RenderLiveFrame();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        ConnectButton.Content = _isConnected ? "Disconnect" : "Connect";
        SavePhotoButton.IsEnabled = _isConnected;
        ExposureSlider.IsEnabled = _isConnected;
        ReturnToLiveButton.IsEnabled = _isConnected && _isShowingSavedPhoto;

        if (!_isConnected)
        {
            MainImage.Source = null;
            OverlayStatusText.Text = "Disconnected";
            return;
        }

        if (!_isShowingSavedPhoto)
        {
            OverlayStatusText.Text = $"Live (Exposure {_exposure})";
        }
    }

    private void RenderLiveFrame()
    {
        if (!_isConnected || _isShowingSavedPhoto)
        {
            return;
        }

        var width = 960;
        var height = 640;
        var stride = width * 4;
        var pixels = new byte[height * stride];
        _frameSeed++;
        var frameShift = _frameSeed % 255;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var idx = (y * stride) + (x * 4);
                var raw = (x + y + frameShift) % 255;
                var adjusted = Math.Clamp(raw * _exposure / 50, 0, 255);
                var px = (byte)adjusted;
                pixels[idx] = (byte)Math.Clamp(px / 2, 0, 255);
                pixels[idx + 1] = px;
                pixels[idx + 2] = px;
                pixels[idx + 3] = 255;
            }
        }

        var frame = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        frame.Freeze();
        _lastLiveFrame = frame;
        MainImage.Source = frame;
        OverlayStatusText.Text = $"Live (Exposure {_exposure})";
    }
}

public sealed record SavedPhoto(string Name, BitmapSource FullImage)
{
    public BitmapSource Thumbnail => FullImage;
}