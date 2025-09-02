using System;
using System.Globalization;
using System.Windows.Data;

namespace AIChatDuo
{
    /// <summary>
    /// 将 0 基索引转换为 1 基显示编号的转换器。
    /// </summary>
    public sealed class IndexPlusOneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i) return (i + 1).ToString(culture);
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && int.TryParse(s, NumberStyles.Integer, culture, out var n))
            {
                return Math.Max(0, n - 1);
            }
            return 0;
        }
    }
}
