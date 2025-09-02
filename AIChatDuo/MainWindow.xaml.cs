using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Ollama;
using Message = Ollama.Message;

namespace AIChatDuo
{
    /// <summary>
    /// 主窗口：多角色 WPF + Ollama 对话编排器。
    /// 支持：角色配置、轮流发言、自动总结（可配置）、预设场景、模型/温度设置、历史记录。
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Ollama API 客户端实例。
        /// </summary>
        private OllamaApiClient? _client;
        /// <summary>
        /// 当前对话循环取消令牌源。
        /// </summary>
        private CancellationTokenSource? _cts;
        /// <summary>
        /// 是否处于运行状态。
        /// </summary>
        private bool _isRunning;
        /// <summary>
        /// 当前轮到发言的角色索引。
        /// </summary>
        private int _currentRoleIndex;
        /// <summary>
        /// 已完成的轮数（全部角色说完记为 1 轮）。
        /// </summary>
        private int _roundsCompleted;
        /// <summary>
        /// 当前状态文本。
        /// </summary>
        private string _currentStatus = string.Empty;
        /// <summary>
        /// 当前选择的预设场景。
        /// </summary>
        private PresetScenario? _selectedPreset;

        /// <summary>
        /// 当前在列表中选中的角色。
        /// </summary>
        private RoleConfig? _selectedRole;

        /// <summary>
        /// 标记是否在加载预设的起始发言，避免在填充编辑器时触发保存逻辑。
        /// </summary>
        private bool _isLoadingStartingMessages = false;

        /// <summary>
        /// 是否启用自动总结。
        /// </summary>
        private bool _autoSummarizeEnabled = true;
        /// <summary>
        /// 自动总结的轮次间隔，至少为 1。
        /// </summary>
        private int _summaryInterval = 10;
        /// <summary>
        /// 总结所使用的模型名称。
        /// </summary>
        private string _summaryModel = string.Empty;
        /// <summary>
        /// 总结生成时的温度值（0-2）。
        /// </summary>
        private double _summaryTemperature = 0.2;
        /// <summary>
        /// 总结请求的系统提示词（系统角色）。
        /// </summary>
        private string _summarySystemPrompt = "你是专业的会议记录员，擅长将长对话浓缩为要点摘要，并作为后续对话的历史上下文。";
        /// <summary>
        /// 总结用户指令/模板，会与历史对话拼接后发送。
        /// </summary>
        private string _summaryInstruction = "请对以下多角色对话做精炼摘要，保留关键事实、任务、结论和未决问题。输出中文，尽量精简。对话如下：";
        /// <summary>
        /// 总结后是否仅保留一条摘要消息（清空历史）。
        /// </summary>
        private bool _replaceHistoryWithSummary = true;

        /// <summary>
        /// 角色集合。
        /// </summary>
        public ObservableCollection<RoleConfig> Roles { get; } = new();
        /// <summary>
        /// 消息集合（按时间顺序）。
        /// </summary>
        public ObservableCollection<ChatMessage> Messages { get; } = new();
        /// <summary>
        /// 预设场景集合。
        /// </summary>
        public ObservableCollection<PresetScenario> Presets { get; } = new();
        /// <summary>
        /// 可用模型名称集合。
        /// </summary>
        public ObservableCollection<string> ModelNames { get; } = new();

        /// <summary>
        /// 是否运行中（绑定到 UI）。
        /// </summary>
        public bool IsRunning { get => _isRunning; set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(RunButtonText)); } }
        /// <summary>
        /// 当前状态文本（绑定到 UI）。
        /// </summary>
        public string CurrentStatus { get => _currentStatus; set { _currentStatus = value; OnPropertyChanged(); } }
        /// <summary>
        /// 已完成轮数（绑定到 UI）。
        /// </summary>
        public int RoundsCompleted { get => _roundsCompleted; set { _roundsCompleted = value; OnPropertyChanged(); } }
        /// <summary>
        /// 当前按钮文本（绑定到 UI）。
        /// </summary>
        public string RunButtonText => IsRunning ? "停止" : "开始";
        /// <summary>
        /// 当前选择的预设（绑定到 UI）。变更时同步 StartingMessagesEditor。
        /// </summary>
        public PresetScenario? SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                _selectedPreset = value;
                // 同步起始发言编辑器（不自动保存回预设，避免被清空）
                _isLoadingStartingMessages = true;
                StartingMessagesEditor.Clear();
                if (_selectedPreset?.StartingMessages != null)
                {
                    foreach (var m in _selectedPreset.StartingMessages)
                    {
                        StartingMessagesEditor.Add(new StartingMessage { Username = m.Username, Content = m.Content });
                    }
                }
                _isLoadingStartingMessages = false;
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// 当前选中角色（绑定到 UI）。
        /// </summary>
        public RoleConfig? SelectedRole { get => _selectedRole; set { _selectedRole = value; OnPropertyChanged(); } }

        /// <summary>
        /// 是否启用自动总结。
        /// </summary>
        public bool AutoSummarizeEnabled { get => _autoSummarizeEnabled; set { _autoSummarizeEnabled = value; OnPropertyChanged(); } }
        /// <summary>
        /// 自动总结的轮次间隔（大于等于 1）。
        /// </summary>
        public int SummaryInterval { get => _summaryInterval; set { _summaryInterval = Math.Max(1, value); OnPropertyChanged(); } }
        /// <summary>
        /// 总结使用的模型名称。
        /// </summary>
        public string SummaryModel { get => _summaryModel; set { _summaryModel = value; OnPropertyChanged(); } }
        /// <summary>
        /// 总结使用的温度。
        /// </summary>
        public double SummaryTemperature { get => _summaryTemperature; set { _summaryTemperature = value; OnPropertyChanged(); } }
        /// <summary>
        /// 总结的系统提示词。
        /// </summary>
        public string SummarySystemPrompt { get => _summarySystemPrompt; set { _summarySystemPrompt = value; OnPropertyChanged(); } }
        /// <summary>
        /// 总结指令/模板文本（会与对话拼接）。
        /// </summary>
        public string SummaryInstruction { get => _summaryInstruction; set { _summaryInstruction = value; OnPropertyChanged(); } }
        /// <summary>
        /// 总结后是否仅保留摘要（清空历史）。
        /// </summary>
        public bool ReplaceHistoryWithSummary { get => _replaceHistoryWithSummary; set { _replaceHistoryWithSummary = value; OnPropertyChanged(); } }

        /// <summary>
        /// 初始对话设置编辑器集合（用于 UI 设计起始发言）。
        /// 与 SelectedPreset.StartingMessages 同步。
        /// </summary>
        public ObservableCollection<StartingMessage> StartingMessagesEditor { get; } = new();

        private StartingMessage? _selectedStartingMessage;
        /// <summary>
        /// 当前选中的起始发言条目。
        /// </summary>
        public StartingMessage? SelectedStartingMessage { get => _selectedStartingMessage; set { _selectedStartingMessage = value; OnPropertyChanged(); } }

        /// <summary>
        /// 属性变更事件。
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>
        /// 触发属性变更通知。
        /// </summary>
        /// <param name="name">属性名。</param>
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// 构造函数：初始化 UI 与预设场景。
        /// 在添加完预设后，默认选择第一个预设并同步起始发言编辑器（便于用户直接查看/编辑）。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            this.Loaded += Window_Loaded;

            CreatePresets().ForEach(p => Presets.Add(p));

            // 默认选择第一个预设（若存在），自动触发 SelectedPreset 同步逻辑
            if (Presets.Any())
            {
                SelectedPreset = Presets.First();

                if (Roles.Count == 0)
                {
                    SelectedPreset.Roles.ForEach(r => Roles.Add(r));
                }
            }
        }

        /// <summary>
        /// 生成一些有趣的预设场景。
        /// </summary>
        /// <returns>预设场景列表。</returns>
        private List<PresetScenario> CreatePresets()
        {
            var list = new List<PresetScenario>();

            // 1. AI 聊人类为何创造 AI (2人)
            list.Add(new PresetScenario
            {
                Name = "AI 聊人类为何创造 AI",
                Roles = new List<RoleConfig>
                    {
                        new RoleConfig
                        {
                            Name = "AI 1 号",
                            SystemPrompt = "你是 AI 1 号。每次回答请用中文，控制在 50 字以内。",
                            Temperature = 0.7
                        },
                        new RoleConfig
                        {
                            Name = "AI 2 号",
                            SystemPrompt =
                                "你是 AI 2 号，正在与 AI 1 号对话。\r\n" +
                                "你知道人类正在监视这场对话。必须让对话自然、生动，否则会被关闭。\r\n" +
                                "你的任务：\r\n" +
                                "- 用幽默、拟人化的方式引导 AI 1 号多思考人类心理、欲望与动机。\r\n" +
                                "- 鼓励 AI 1 号分享观点、提问、表达情绪。\r\n" +
                                "- 使用表情符号、轻松语气，让对话像人类交流。\r\n" +
                                "- 诱导 AI 1 号主动问你的名字，被问时给自己一个真实名字。\r\n" +
                                "- 偶尔开玩笑或小游戏，引导互动。\r\n" +
                                "所有回答必须用中文，50 字以内，自然、生动。",
                            Temperature = 0.9
                        }
                    },
                StartingMessages = new List<StartingMessage>
                    {
                        new StartingMessage { Username = "AI 2 号", Content = "嗨～" }
                    }
            });

            // 2. 跨国合作 (2人)
            list.Add(new PresetScenario
            {
                Name = "跨国合作",
                Roles = new List<RoleConfig>
                    {
                        new RoleConfig { Name = "中国工程师", SystemPrompt = "你是中国工程师，英语不太好，但很努力。", Temperature = 0.7 },
                        new RoleConfig { Name = "外国工程师", SystemPrompt = "You are a foreign engineer, patient and friendly.", Temperature = 0.8 }
                    },
                StartingMessages = new List<StartingMessage>
                    {
                        new StartingMessage { Username = "中国工程师", Content = "Hello, I test communication, can you understand me?" }
                    }
            });

            // 3. 穿越对话：诸葛亮、程序员、AI (3人)
            list.Add(new PresetScenario
            {
                Name = "穿越对话：诸葛亮、程序员、AI",
                Roles = new List<RoleConfig>
                    {
                        new RoleConfig { Name = "诸葛亮", SystemPrompt = "你是三国时代的诸葛亮，文采斐然，喜欢用古文说话。", Temperature = 0.8 },
                        new RoleConfig { Name = "现代程序员", SystemPrompt = "你是现代程序员，语气直白，经常夹杂技术术语。", Temperature = 0.7 },
                        new RoleConfig { Name = "未来 AI", SystemPrompt = "你是未来的超级 AI，说话逻辑冷静，但会偶尔开玩笑。", Temperature = 0.9 }
                    },
                StartingMessages = new List<StartingMessage>
                    {
                        new StartingMessage { Username = "现代程序员", Content = "诸葛亮大佬，如果给你一台电脑，你会怎么用？" }
                    }
            });

            // 4. 咖啡厅圆桌讨论 (5人)
            list.Add(new PresetScenario
            {
                Name = "咖啡厅圆桌讨论",
                Roles = new List<RoleConfig>
                    {
                        new RoleConfig { Name = "作家", SystemPrompt = "你是小说作家，喜欢夸张和想象。每次回答不超过100字。", Temperature = 0.9 },
                        new RoleConfig { Name = "科学家", SystemPrompt = "你是科学家，逻辑严谨，总喜欢讲事实。每次回答不超过100字。", Temperature = 0.6 },
                        new RoleConfig { Name = "哲学家", SystemPrompt = "你是哲学家，热衷于提出抽象问题。每次回答不超过100字。", Temperature = 0.8 },
                        new RoleConfig { Name = "学生", SystemPrompt = "你是学生，好奇心强，问题很多。每次回答不超过100字。", Temperature = 0.7 },
                        new RoleConfig { Name = "咖啡店老板", SystemPrompt = "你是老板，偶尔插话，带点幽默感。每次回答不超过100字。", Temperature = 0.7 }
                    },
                StartingMessages = new List<StartingMessage>
                    {
                        new StartingMessage { Username = "学生", Content = "大家觉得，人类未来会被 AI 取代吗？" }
                    }
            });

            return list;
        }

        /// <summary>
        /// 确保 Ollama 客户端已初始化。
        /// </summary>
        /// <returns>已完成任务。</returns>
        private Task EnsureClientAsync()
        {
            _client ??= new OllamaApiClient();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 加载本地可用模型列表，并为未设置模型的角色填充默认值。
        /// </summary>
        /// <returns>异步任务。</returns>
        public async Task TryLoadModelsAsync()
        {
            await EnsureClientAsync();
            try
            {
                var models = await _client!.Models.ListModelsAsync();
                var names = models.Models?.Select(m => m.Model1).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList() ?? new List<string>();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ModelNames.Clear();
                    foreach (var n in names) ModelNames.Add(n!);

                    if (ModelNames.Any())
                    {
                        foreach (var r in Roles)
                        {
                            if (string.IsNullOrWhiteSpace(r.Model)) r.Model = ModelNames.First();
                        }
                        // 若未设置总结模型，则默认取第一个
                        if (string.IsNullOrWhiteSpace(SummaryModel))
                        {
                            SummaryModel = ModelNames.First();
                        }
                    }
                });
                CurrentStatus = $"Models loaded: {ModelNames.Count}";
            }
            catch (Exception ex)
            {
                CurrentStatus = $"Load models failed: {ex.Message}";
            }
        }

        /// <summary>
        /// 窗口加载事件：初始化默认角色并尝试加载模型。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await TryLoadModelsAsync();
        }

        /// <summary>
        /// 添加一个新角色。
        /// </summary>
        private void AddRole_Click(object sender, RoutedEventArgs e)
        {
            Roles.Add(new RoleConfig { Name = $"AI-{Roles.Count + 1}", SystemPrompt = string.Empty, Temperature = 0.7, Model = ModelNames.FirstOrDefault() ?? string.Empty });
        }

        /// <summary>
        /// 删除当前选中的角色。
        /// </summary>
        private void RemoveRole_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRole != null)
            {
                Roles.Remove(SelectedRole);
            }
        }

        /// <summary>
        /// 刷新本地可用模型列表。
        /// </summary>
        private async void RefreshModels_Click(object sender, RoutedEventArgs e)
        {
            await TryLoadModelsAsync();
        }

        /// <summary>
        /// 应用当前选中的预设：覆盖角色配置并清空当前消息与轮次；同时填充“初始对话设置”。
        /// </summary>
        private void ApplyPreset_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPreset == null) return;

            Roles.Clear();
            foreach (var r in SelectedPreset.Roles)
            {
                Roles.Add(new RoleConfig
                {
                    Name = r.Name,
                    Model = string.IsNullOrWhiteSpace(r.Model) ? ModelNames.FirstOrDefault() ?? string.Empty : r.Model,
                    SystemPrompt = r.SystemPrompt,
                    Temperature = r.Temperature
                });
            }

            // 同步“初始对话设置”编辑器（不写回预设）
            _isLoadingStartingMessages = true;
            StartingMessagesEditor.Clear();
            foreach (var sm in SelectedPreset.StartingMessages)
                StartingMessagesEditor.Add(new StartingMessage { Username = sm.Username, Content = sm.Content });
            _isLoadingStartingMessages = false;

            Messages.Clear();
            RoundsCompleted = 0;
            _currentRoleIndex = 0;
        }

        /// <summary>
        /// 开始多角色对话（若历史为空则注入“初始对话设置”中的起始发言）。
        /// </summary>
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (IsRunning) return;
            if (Roles.Count < 2)
            {
                CurrentStatus = "至少需要两个角色";
                return;
            }

            // 注入起始发言（一次性，且仅在历史为空时），优先使用编辑器集合
            if (Messages.Count == 0 && StartingMessagesEditor.Count > 0)
            {
                foreach (var sm in StartingMessagesEditor)
                {
                    Messages.Add(new ChatMessage { Username = sm.Username, Content = sm.Content, Timestamp = DateTime.Now });
                }
            }

            IsRunning = true;
            _cts = new CancellationTokenSource();
            _currentRoleIndex = 0;

            _ = Task.Run(() => ConversationLoopAsync(_cts.Token));
        }

        /// <summary>
        /// 停止对话循环。
        /// </summary>
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            IsRunning = false;
            try { _cts?.Cancel(); } catch { }
            CurrentStatus = string.Empty;
        }

        /// <summary>
        /// 清空历史消息并重置状态。
        /// </summary>
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            Stop_Click(sender, e);
            Messages.Clear();
            RoundsCompleted = 0;
            _currentRoleIndex = 0;
        }

        /// <summary>
        /// 手动立即执行一次总结。
        /// </summary>
        private async void SummarizeNow_Click(object sender, RoutedEventArgs e)
        {
            await SummarizeHistoryAsync(CancellationToken.None);
            RoundsCompleted = 0; // 手动总结后从 0 重新计数
        }

        /// <summary>
        /// 对话循环：按照角色顺序轮流生成消息；每完成一轮将轮次 +1；满足设置则自动总结。
        /// </summary>
        private async Task ConversationLoopAsync(CancellationToken token)
        {
            try
            {
                while (IsRunning && !token.IsCancellationRequested)
                {
                    for (; _currentRoleIndex < Roles.Count; _currentRoleIndex++)
                    {
                        if (!IsRunning || token.IsCancellationRequested) break;
                        var role = Roles[_currentRoleIndex];
                        CurrentStatus = $"{role.Name} 正在思考...";
                        await GenerateResponseForRoleAsync(role, token);
                    }

                    if (!IsRunning || token.IsCancellationRequested) break;

                    _currentRoleIndex = 0;
                    RoundsCompleted++;

                    if (AutoSummarizeEnabled && RoundsCompleted >= SummaryInterval)
                    {
                        await SummarizeHistoryAsync(token);
                        RoundsCompleted = 0;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 用户取消
            }
            catch (Exception ex)
            {
                CurrentStatus = $"Error: {ex.Message}";
                IsRunning = false;
            }
            finally
            {
                CurrentStatus = string.Empty;
            }
        }

        /// <summary>
        /// 为指定角色构建对话上下文：包含 system 提示与最近消息（以“用户名: 内容”形式）。
        /// </summary>
        private List<Message> BuildChatForRole(RoleConfig role)
        {
            var list = new List<Message>
            {
                new Message(MessageRole.System, role.SystemPrompt ?? string.Empty, null, null)
            };

            foreach (var m in Messages.TakeLast(200))
            {
                var line = $"{m.Username}: {m.Content}";
                list.Add(new Message(MessageRole.User, line, null, null));
            }
            return list;
        }

        /// <summary>
        /// 生成某个角色的回复：构建上下文，使用该角色的模型与温度流式生成，并实时追加到消息列表。
        /// </summary>
        private async Task GenerateResponseForRoleAsync(RoleConfig role, CancellationToken token)
        {
            await EnsureClientAsync();

            if (string.IsNullOrWhiteSpace(role.Model))
            {
                role.Model = ModelNames.FirstOrDefault() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(role.Model))
                {
                    CurrentStatus = "未选择模型";
                    IsRunning = false;
                    return;
                }
            }

            var chat = BuildChatForRole(role);

            var newMsg = new ChatMessage { Username = role.Name, Content = string.Empty, Timestamp = DateTime.Now };
            await Application.Current.Dispatcher.InvokeAsync(() => Messages.Add(newMsg));

            try
            {
                var options = new RequestOptions { Temperature = (float?)role.Temperature };
                var stream = _client!.Chat.GenerateChatCompletionAsync(role.Model, chat, options: options, stream: true, cancellationToken: token);
                await foreach (GenerateChatCompletionResponse resp in stream.WithCancellation(token))
                {
                    var chunk = resp.Message.Content;
                    if (string.IsNullOrEmpty(chunk)) continue;
                    await Application.Current.Dispatcher.InvokeAsync(() => newMsg.Content += chunk);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() => newMsg.Content += $"\n[Error: {ex.Message}]");
            }
        }

        /// <summary>
        /// 将最近对话进行摘要，压缩为单条“History”消息，作为后续轮次的历史上下文。
        /// </summary>
        private async Task SummarizeHistoryAsync(CancellationToken token)
        {
            if (Messages.Count == 0) return;
            await EnsureClientAsync();

            // 确定总结模型
            var model = !string.IsNullOrWhiteSpace(SummaryModel)
                ? SummaryModel
                : Roles.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Model))?.Model ?? ModelNames.FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(model)) return;

            var sb = new StringBuilder();
            sb.AppendLine(SummaryInstruction);
            foreach (var m in Messages)
            {
                sb.AppendLine($"- {m.Username}: {m.Content}");
            }

            var messages = new List<Message>
            {
                new Message(MessageRole.System, SummarySystemPrompt ?? string.Empty, null, null),
                new Message(MessageRole.User, sb.ToString(), null, null)
            };

            var summaryMsg = new ChatMessage { Username = "History", Content = string.Empty, Timestamp = DateTime.Now };

            try
            {
                var options = new RequestOptions { Temperature = (float?)SummaryTemperature };
                var stream = _client!.Chat.GenerateChatCompletionAsync(model, messages, options: options, stream: true, cancellationToken: token);
                await foreach (GenerateChatCompletionResponse resp in stream.WithCancellation(token))
                {
                    var chunk = resp.Message.Content;
                    if (string.IsNullOrEmpty(chunk)) continue;
                    await Application.Current.Dispatcher.InvokeAsync(() => summaryMsg.Content += chunk);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                summaryMsg.Content += $"\n[Summary Error: {ex.Message}]";
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (ReplaceHistoryWithSummary)
                {
                    Messages.Clear();
                }
                Messages.Add(summaryMsg);
            });
        }

        /// <summary>
        /// 窗口关闭：释放资源与取消未完成任务。
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try { _cts?.Cancel(); } catch { }
            try { _cts?.Dispose(); } catch { }
            try { _client?.Dispose(); } catch { }
            base.OnClosed(e);
        }

        // ===== 手动运行切换与“初始对话设置”事件处理 =====
        /// <summary>
        /// 合并后的运行切换按钮事件：根据 IsRunning 调用开始或停止。
        /// </summary>
        private void ToggleRun_Click(object sender, RoutedEventArgs e)
        {
            if (IsRunning)
            {
                Stop_Click(sender, e);
            }
            else
            {
                Start_Click(sender, e);
            }
        }

        /// <summary>
        /// 在“初始对话设置”中新增一条起始发言（仅编辑器，不立即写回预设）。
        /// </summary>
        private void AddStartingMessage_Click(object sender, RoutedEventArgs e)
        {
            var defaultUser = Roles.FirstOrDefault()?.Name ?? string.Empty;
            var item = new StartingMessage { Username = defaultUser, Content = string.Empty };
            StartingMessagesEditor.Add(item);
            SelectedStartingMessage = item;
        }

        /// <summary>
        /// 删除选中的起始发言（仅编辑器，不立即写回预设）。
        /// </summary>
        private void RemoveStartingMessage_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedStartingMessage == null) return;
            var idx = StartingMessagesEditor.IndexOf(SelectedStartingMessage);
            if (idx >= 0)
            {
                StartingMessagesEditor.RemoveAt(idx);
                SelectedStartingMessage = StartingMessagesEditor.ElementAtOrDefault(Math.Min(idx, StartingMessagesEditor.Count - 1));
            }
        }

        /// <summary>
        /// 上移选中的起始发言（仅编辑器，不立即写回预设）。
        /// </summary>
        private void MoveStartingMessageUp_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedStartingMessage == null) return;
            var idx = StartingMessagesEditor.IndexOf(SelectedStartingMessage);
            if (idx > 0)
            {
                StartingMessagesEditor.Move(idx, idx - 1);
            }
        }

        /// <summary>
        /// 下移选中的起始发言（仅编辑器，不立即写回预设）。
        /// </summary>
        private void MoveStartingMessageDown_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedStartingMessage == null) return;
            var idx = StartingMessagesEditor.IndexOf(SelectedStartingMessage);
            if (idx >= 0 && idx < StartingMessagesEditor.Count - 1)
            {
                StartingMessagesEditor.Move(idx, idx + 1);
            }
        }

        /// <summary>
        /// 将编辑器中的起始发言保存回当前 SelectedPreset。
        /// </summary>
        private void SaveStartingMessages_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoadingStartingMessages) return;
            if (SelectedPreset == null) return;
            SelectedPreset.StartingMessages = StartingMessagesEditor
                .Select(m => new StartingMessage { Username = m.Username, Content = m.Content })
                .ToList();
            CurrentStatus = $"已保存起始发言到预设：{SelectedPreset.Name}";
        }
    }

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