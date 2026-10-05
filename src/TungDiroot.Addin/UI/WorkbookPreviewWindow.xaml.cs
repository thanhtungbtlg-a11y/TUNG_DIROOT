using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using TungDiroot.Presentation.UI;

namespace TungDiroot.Addin.UI;

public partial class WorkbookPreviewWindow : Window
{
    public WorkbookPreviewWindow(WorkbookPreviewViewModel viewModel, byte[] pngBytes)
    {
        if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
        if (pngBytes == null) throw new ArgumentNullException(nameof(pngBytes));
        InitializeComponent();
        DataContext = viewModel;
        PreviewImage.Source = LoadBitmap(pngBytes);
    }

    private static BitmapImage LoadBitmap(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

