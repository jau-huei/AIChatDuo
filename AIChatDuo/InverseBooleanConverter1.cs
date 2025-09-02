using System.Windows.Data;

namespace AIChatDuo
{
    /// <summary>
    /// 取反布尔值的转换器：true -> false, false -> true。用于根据 IsRunning 锁定输入控件。
    /// </summary>
    public sealed class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b) return !b;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
    }
}