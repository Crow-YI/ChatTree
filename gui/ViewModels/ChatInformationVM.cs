using System.Net.Http;
using System.Windows;
using TreeChat.Commands;
using TreeChat.Infrastructure;
using TreeChat.Models;

namespace TreeChat.ViewModels
{
    /// <summary>
    /// 节点信息VM
    /// </summary>
    public class ChatInformationVM : BaseViewModel
    {
        private string? _userMessage;
        private string? _aiReply;
        private string? _systemPrompt;
        private bool _isRootSelected;

        public string? UserMessage
        {
            get => _userMessage;
            set => SetProperty(ref _userMessage, value);
        }
        public string? AIReply
        {
            get => _aiReply;
            set => SetProperty(ref _aiReply, value);
        }

        /// <summary>
        /// 系统提示词（选中根节点时编辑）
        /// </summary>
        public string? SystemPrompt
        {
            get => _systemPrompt;
            set
            {
                if (SetProperty(ref _systemPrompt, value) && CurrentChatTree != null)
                {
                    CurrentChatTree.SystemPrompt = value ?? "";
                }
            }
        }

        /// <summary>
        /// 当前选中的节点是否为根节点
        /// </summary>
        public bool IsRootSelected
        {
            get => _isRootSelected;
            set => SetProperty(ref _isRootSelected, value);
        }

        private string _inputMessage;
        public string InputMessage
        {
            get => _inputMessage;
            set
            {
                SetProperty(ref _inputMessage, value);
                SendMessage.OnCanExecuteChanged();
            }
        }

        public AsyncRelayCommand SendMessage { get; }

        private TreeNodeVM? _selectedNode;
        public TreeNodeVM? SelectedNode
        {
            get => _selectedNode;
            set
            {
                _selectedNode = value;
                if(value != null)
                {
                    bool isRoot = value.Node.ParentNode == null;
                    IsRootSelected = isRoot;

                    if (isRoot)
                    {
                        // 根节点：显示系统信息编辑器
                        SystemPrompt = CurrentChatTree?.SystemPrompt ?? "";
                        UserMessage = null;
                        AIReply = null;
                    }
                    else
                    {
                        // 非根节点：显示 Q&A
                        UserMessage = value.Node.UserMessage;
                        AIReply = value.Node.ReplyMessage;
                    }
                }
                SendMessage.OnCanExecuteChanged();
            }
        }

        private ChatTree? _currentChatTree;
        public ChatTree? CurrentChatTree
        {
            get => _currentChatTree;
            set => SetProperty(ref _currentChatTree, value);
        }

        public event Action<TreeNodeVM, TreeNodeVM>? ChatTreeChanged;

        public ChatInformationVM()
        {
            UserMessage = string.Empty;
            AIReply = string.Empty;
            SystemPrompt = string.Empty;

            SendMessage = new AsyncRelayCommand(
                execute: ExecuteSendMessageAsync,
                canExecute: CanExecuteSendMessage);
        }

        private bool CanExecuteSendMessage(object? parameter)
        {
            return !string.IsNullOrEmpty(InputMessage) && SelectedNode != null && CurrentChatTree != null;
        }

        private async Task ExecuteSendMessageAsync(object? parameter)
        {
            if (string.IsNullOrWhiteSpace(InputMessage) || SelectedNode == null || CurrentChatTree == null) return;

            TreeNodeVM previousSelected = SelectedNode;
            TreeNodeVM? newNodeVM = null;

            // 在循环外保存用户消息，避免 InputMessage 清空后重试时丢失
            var userMessage = InputMessage;
            if (string.IsNullOrWhiteSpace(userMessage)) return;

            // 最多尝试 2 次（含首次 + 一次自动重试）
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                AppLogger.Info(
                    "Chat message sending: tree={TreeId} parent={ParentId} attempt={Attempt}",
                    CurrentChatTree?.TreeId, SelectedNode.Node.NodeID, attempt);
                try
                {
                    // 乐观 UI 更新：先创建本地临时节点（使用树内计数器获取 ID）
                    int newNodeId = CurrentChatTree.GetNextNodeId();
                    ChatTreeNode newNode = new ChatTreeNode(SelectedNode.Node, userMessage, newNodeId);
                    newNodeVM = SelectedNode.AddChild(newNode);
                    CurrentChatTree.IsModified = true;  // 标记未保存更改
                    if (attempt == 1)
                    {
                        // 仅在第一次尝试时清空输入框
                        InputMessage = string.Empty;
                    }
                    ChatTreeChanged?.Invoke(SelectedNode, newNodeVM);
                    SelectedNode = newNodeVM;
                    AIReply = "";

                    // 通过 Python 后端发送（流式）
                    // 首次发送时自动在 Python 端创建对话树
                    var backend = App.Backend;
                    if (string.IsNullOrEmpty(CurrentChatTree.TreeId))
                    {
                        var treeResponse = await backend.CreateTreeAsync(
                            CurrentChatTree.TreeTitle,
                            CurrentChatTree.SystemPrompt);
                        CurrentChatTree.TreeId = treeResponse.TreeId;
                    }
                    string treeId = CurrentChatTree.TreeId!;

                    string fullContent = "";
                    // 在后台线程上消费 SSE 流，避免阻塞 UI 线程渲染
                    var sseEvents = backend.ChatStreamAsync(
                        treeId, previousSelected.Node.NodeID, userMessage);
                    await Task.Run(async () =>
                    {
                        await foreach (var sseEvent in sseEvents)
                        {
                            switch (sseEvent.EventType)
                            {
                                case "created":
                                    break;

                                case "delta":
                                    var delta = System.Text.Json.JsonSerializer.Deserialize<Models.SseDeltaEvent>(sseEvent.Data);
                                    if (delta != null)
                                    {
                                        fullContent += delta.Content;
                                        var contentToShow = fullContent;
                                        // 以 Background 优先级调度（低于 DataBind 和 Render），
                                        // 确保上一轮 DataBind(8)→Render(7) 完成后再更新
                                        await Application.Current.Dispatcher.InvokeAsync(() =>
                                        {
                                            AIReply = contentToShow;
                                        }, System.Windows.Threading.DispatcherPriority.Background);
                                    }
                                    break;

                                case "done":
                                    var done = System.Text.Json.JsonSerializer.Deserialize<Models.SseDoneEvent>(sseEvent.Data);
                                    AppLogger.Info(
                                        "Chat message done: tree={TreeId} node={NodeId}",
                                        CurrentChatTree?.TreeId, done?.NodeId);
                                    await Application.Current.Dispatcher.InvokeAsync(() =>
                                    {
                                        if (done?.ReplyMessage != null)
                                        {
                                            SelectedNode.Node.SetAiReply(done.ReplyMessage.Content);
                                            CurrentChatTree.IsModified = true;  // 标记未保存更改
                                        }
                                        // 触发树更新
                                        ChatTreeChanged?.Invoke(SelectedNode, SelectedNode);
                                    });
                                    return;

                                case "error":
                                    var error = System.Text.Json.JsonSerializer.Deserialize<Models.SseErrorEvent>(sseEvent.Data);
                                    AppLogger.Warn(
                                        "SSE error event: key={Key} message={Message} detail={Detail}",
                                        error?.Key, error?.Message, error?.Detail);
                                    await Application.Current.Dispatcher.InvokeAsync(() =>
                                    {
                                        // 失败清理
                                        previousSelected.RemoveChild(newNodeVM);
                                        SelectedNode = previousSelected;
                                        ChatTreeChanged?.Invoke(SelectedNode, SelectedNode);
                                        AIReply = string.Empty;
                                        MessageBox.Show(
                                            error?.Message ?? "AI 调用失败。",
                                            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                    });
                                    return;
                            }
                        }
                    });

                    return; // 成功，退出
                }
                catch (HttpRequestException ex)
                {
                    // 清理乐观创建的节点
                    if (newNodeVM != null && previousSelected.Children.Contains(newNodeVM))
                    {
                        previousSelected.RemoveChild(newNodeVM);
                    }
                    SelectedNode = previousSelected;
                    ChatTreeChanged?.Invoke(SelectedNode, SelectedNode);
                    AIReply = string.Empty;

                    // 第一次失败时尝试重启后端
                    if (attempt == 1)
                    {
                        AppLogger.Warn("Backend connection failed, attempting restart...");
                        var restarted = await App.Backend.TryRestartAsync();
                        if (restarted)
                        {
                            AppLogger.Info("Backend restarted, retrying chat...");
                            continue; // 重试
                        }
                    }

                    AppLogger.Error(ex, "Chat failed after retry");
                    MessageBox.Show($"无法连接到 Python 后端服务。\n\n{ex.Message}",
                        "连接错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "Chat failed with unexpected error: tree={TreeId}",
                        CurrentChatTree?.TreeId);
                    if (newNodeVM != null && previousSelected.Children.Contains(newNodeVM))
                    {
                        previousSelected.RemoveChild(newNodeVM);
                    }
                    SelectedNode = previousSelected;
                    ChatTreeChanged?.Invoke(SelectedNode, SelectedNode);
                    AIReply = string.Empty;
                    MessageBox.Show($"请求过程中发生错误：{ex.Message}",
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
        }
    }
}
