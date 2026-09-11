using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GotifyClient
{
    // Resolves the icon Border's Background: the app's own image when Gotify
    // supplied one, otherwise a solid priority-color fallback.
    public class AppIconBrushConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string imageUrl = values.Length > 0 ? values[0] as string : null;
            string priorityColorHex = values.Length > 1 ? values[1] as string : null;

            if (!string.IsNullOrEmpty(imageUrl))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imageUrl, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnDemand;
                    bitmap.EndInit();
                    return new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                }
                catch
                {
                    // Malformed URL - fall through to the priority-color fallback.
                    // A runtime download failure (404/network) after this point is not
                    // handled - accepted limitation, out of scope (presence check only).
                }
            }

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(priorityColorHex ?? "#6B7280");
                return new SolidColorBrush(color);
            }
            catch
            {
                return new SolidColorBrush(Colors.Gray);
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
