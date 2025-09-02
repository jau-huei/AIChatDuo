using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AIChatDuo
{
    /// <summary>
    /// 聊天消息：包含发送者、内容与时间戳。
    /// </summary>
    public class ChatMessage : INotifyPropertyChanged
    {
        private string _username = string.Empty;
        private string _content = string.Empty;
        private DateTime _timestamp = DateTime.Now;

        /// <summary>
        /// 发送者名称（角色名或 History）。
        /// </summary>
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }
        /// <summary>
        /// 消息内容。
        /// </summary>
        public string Content { get => _content; set { _content = value; OnPropertyChanged(); } }
        /// <summary>
        /// 时间戳。
        /// </summary>
        public DateTime Timestamp { get => _timestamp; set { _timestamp = value; OnPropertyChanged(); } }

        /// <summary>
        /// 属性变更事件。
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>
        /// 触发属性变更通知。
        /// </summary>
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}