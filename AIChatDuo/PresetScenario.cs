namespace AIChatDuo
{
    /// <summary>
    /// 预设场景：可定义多个角色与起始发言。
    /// </summary>
    public class PresetScenario
    {
        /// <summary>
        /// 预设名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 角色列表。
        /// </summary>
        public List<RoleConfig> Roles { get; set; } = new();
        /// <summary>
        /// 起始发言列表（开始对话且历史为空时注入）。
        /// </summary>
        public List<StartingMessage> StartingMessages { get; set; } = new();
    }
}