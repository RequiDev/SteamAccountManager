using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace SteamAccountManager.App.Converters;

/// <summary>
/// Converts an avatar file path to a frozen <see cref="BitmapImage"/>. Loads with OnLoad caching and
/// IgnoreImageCache so the file handle is released immediately and updated files are re-read. Falls back
/// to the packaged placeholder avatar when the path is null/empty/missing.
/// </summary>
public sealed class PathToImageConverter : IValueConverter
{
    private const string PlaceholderUri =
        "pack://application:,,,/SteamAccountManager.App;component/Assets/placeholder-avatar.png";

    private static readonly BitmapImage Placeholder = LoadPlaceholder();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var path = value as string;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return Placeholder;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze(); // releases the file handle and makes it usable across threads
            return bitmap;
        }
        catch
        {
            return Placeholder;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static BitmapImage LoadPlaceholder()
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(PlaceholderUri, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
