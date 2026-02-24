using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace iPhoneTransfer.UI.Converters;

/// <summary>
/// Converts file size in bytes to human-readable format (KB, MB, GB).
/// WHY: Display "2.5 MB" instead of "2621440" in UI.
/// </summary>
public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        return "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts byte array (thumbnail data) to BitmapImage for WPF display.
/// WHY: WPF Image control requires BitmapImage, not byte[].
/// </summary>
public class ByteArrayToImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is byte[] bytes && bytes.Length > 0)
        {
            try
            {
                var image = new System.Windows.Media.Imaging.BitmapImage();
                using var mem = new System.IO.MemoryStream(bytes);
                
                image.BeginInit();
                image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                image.StreamSource = mem;
                image.EndInit();
                image.Freeze(); // WHY: Freeze for thread safety and performance

                return image;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts boolean value.
/// WHY: Show UI element when condition is FALSE.
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }

        return false;
    }
}

/// <summary>
/// Converts null to Visibility.
/// WHY: Show/hide UI elements based on null value.
/// Parameter="Inverted" to reverse logic.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isInverted = parameter?.ToString()?.Equals("Inverted", StringComparison.OrdinalIgnoreCase) ?? false;
        bool isNull = value == null;

        if (isInverted)
        {
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts enum value to Visibility.
/// WHY: Show UI element only when enum matches parameter.
/// </summary>
public class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
        {
            return Visibility.Collapsed;
        }

        string enumValue = value.ToString() ?? "";
        string targetValue = parameter.ToString() ?? "";

        return enumValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Checks if integer value is zero.
/// WHY: Show "no items" message when collection count is 0.
/// </summary>
public class IsZeroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue == 0;
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
