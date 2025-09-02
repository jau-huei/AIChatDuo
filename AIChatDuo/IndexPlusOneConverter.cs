using System;
using System.Globalization;
using System.Windows.Data;

namespace AIChatDuo
{
    /// <summary>
    /// �� 0 ������ת��Ϊ 1 ����ʾ��ŵ�ת������
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
