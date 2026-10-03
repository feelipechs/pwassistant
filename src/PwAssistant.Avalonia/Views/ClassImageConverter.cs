using System.Collections.Concurrent;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PwAssistant.Avalonia.Views;

/// <summary>
/// Loads class art via AssetLoader instead of relying on the binding
/// engine's string→Bitmap conversion (which silently yields blank images
/// for avares:// URIs). Results are cached: rows rebuild often (RebuildAll)
/// and each Bitmap holds a decoded Skia image. Missing art → null (the
/// paired IsVisible binding collapses the frame), never an exception.
/// </summary>
public sealed class ClassImageConverter : IValueConverter
{
    public static ClassImageConverter Instance { get; } = new();

    private static readonly ConcurrentDictionary<string, Bitmap?> Cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return null;
        return Cache.GetOrAdd(path, Load);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Bitmap? Load(string path)
    {
        try
        {
            // Kept open for the Bitmap lifetime (Skia decodes lazily).
            Stream stream = AssetLoader.Open(new Uri(path));
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
