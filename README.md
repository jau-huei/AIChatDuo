# AIChatDuo - AI 多角色对话 (Ollama)

一个基于 .NET 8 WPF 的本地多角色 AI 对话编排器，集成 Ollama。本项目支持多角色轮流发言、预设场景、初始对话设置与自动总结，可用于模拟多角色协作、头脑风暴与场景化沟通。

![Screenshot](https://github.com/jau-huei/AIChatDuo/blob/master/AIChatDuo/img.png)

## 功能特性
- 多角色轮流发言：支持每个角色独立的模型、系统提示词与温度。
- 预设场景：一键加载“角色列表 + 初始发言”。
- 初始对话设置（新）：
  - 单独的 Tab 页进行编辑。
  - 左侧列表以数字编号显示条目，右侧可编辑“用户名（角色名）+ 内容”。
  - 通过“保存到预设”手动写回当前预设，避免误覆盖。
- 自动总结：
  - 可配置每 N 轮自动总结；可选“仅保留摘要（清空历史）”。
  - 可配置“总结模型”“温度”“系统提示词”“摘要指令/模板”。
  - 支持手动触发总结（若顶部未显示按钮，请在代码中启用）。
- 模型管理：刷新本地模型列表，自动为未设置模型的角色填充默认值。
- 运行锁定：对话运行中自动锁定所有配置输入，避免误操作。
- UI 优化：角色设置/总结设置/初始对话设置 分离为 Tab；按钮统一最小宽度；列表项编号友好。

## 运行环境
- Windows 10/11
- .NET SDK 8.0+
- Visual Studio 2022（推荐）或命令行 dotnet CLI
- 本机已安装并运行 Ollama（https://ollama.com/）
  - 请确保已在 Ollama 中拉取或存在对应的模型（如 qwen2、llama3 等）。

## 构建与运行
- 使用 Visual Studio：打开解决方案并直接构建运行。
- 使用命令行：
  ```bash
  dotnet build
  dotnet run --project AIChatDuo/AIChatDuo.csproj
  ```

## 使用说明
1. 顶部选择“预设场景”，点击“应用预设”。这会填充：
   - 左侧角色列表（名称、模型、系统提示词、温度）。
   - “初始对话设置”Tab 中的起始发言列表（仅编辑器，不立即写回）。
2. 如需修改起始发言：
   - 打开“初始对话设置”Tab，编辑左侧列表与右侧详情。
   - 点击“保存到预设”将当前编辑器内容写回所选预设。
3. 配置“自动总结设置”Tab 中的策略与提示词模板（可选）。
4. 点击“开始/停止”（同一按钮）启动/停止多角色轮流对话。
   - 运行中，所有输入将被锁定（不可编辑）。
   - 若当前历史为空，将把“初始对话设置”的内容注入为对话开场。
5. 点击“清空”可清除历史并重置轮次；“刷新模型”可重新读取本地 Ollama 模型列表。

## 主要文件
- 界面：
  - AIChatDuo/MainWindow.xaml
  - AIChatDuo/MainWindow.xaml.cs
- 转换器：
  - AIChatDuo/InverseBooleanConverter.cs（运行状态取反，用于锁定输入）
  - AIChatDuo/IndexPlusOneConverter.cs（列表项索引 0->1 显示）
- 核心数据结构（位于 MainWindow.xaml.cs）：
  - RoleConfig：角色配置（Name/Model/SystemPrompt/Temperature）
  - ChatMessage：聊天消息（Username/Content/Timestamp）
  - PresetScenario：预设场景（Name/Roles/StartingMessages）
  - StartingMessage：起始发言（Username/Content）

## 常见问题
- 模型下拉为空/未选择模型：
  - 请确保本机 Ollama 正在运行且已安装至少一个模型；点击“刷新模型”。
- 运行中无法修改配置：
  - 设计如此，防止运行时更改配置导致异常。请停止后再修改。
- 预设被意外覆盖：
  - 现在仅在“初始对话设置”中点击“保存到预设”才会写回，避免误覆盖。
