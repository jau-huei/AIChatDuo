using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AIChatDuo
{
    /// <summary>
    /// 角色配置：名称、模型、系统提示词与温度。
    /// </summary>
    public class RoleConfig : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _model = string.Empty;
        private string _systemPrompt = string.Empty;
        private double _temperature = 0.7;

        /// <summary>
        /// 角色名称（显示用）。
        /// </summary>
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        /// <summary>
        /// 模型名称（如：qwen2:7b）。
        /// </summary>
        public string Model { get => _model; set { _model = value; OnPropertyChanged(); } }
        /// <summary>
        /// 系统提示词（System Prompt）。
        /// </summary>
        public string SystemPrompt { get => _systemPrompt; set { _systemPrompt = value; OnPropertyChanged(); } }
        /// <summary>
        /// 温度值（0-2，越大越有创造性）。
        /// </summary>
        public double Temperature { get => _temperature; set { _temperature = value; OnPropertyChanged(); } }

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