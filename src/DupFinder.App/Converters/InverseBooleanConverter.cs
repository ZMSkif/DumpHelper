using System.Globalization;
using System.Windows.Data;

namespace DupFinder.App.Converters;

/// <summary>Инвертирует логическое значение: «защищён» → «галочку ставить нельзя».</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag ? !flag : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag ? !flag : true;
}
