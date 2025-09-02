using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AIChatDuo
{
    /// <summary>
    /// 起始发言：用户名 + 文本。
    /// </summary>
    public class StartingMessage : INotifyPropertyChanged
    {
        private string _username = string.Empty;
        private string _content = string.Empty;

        /// <summary>
        /// 发送者用户名（应与角色名对应）。
        /// </summary>
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }
        /// <summary>
        /// 发言内容。
        /// </summary>
        public string Content { get => _content; set { _content = value; OnPropertyChanged(); } }

        /// <summary>
        /// 属性变更事件。
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}