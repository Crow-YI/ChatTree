# TreeChat v2.0

基于 Python 后端 + WPF 前端的树形 AI 聊天工具。

## 项目结构

```
TreeChat/
│
├── .gitignore                   # 忽略规则：Python 产物、.env、IDE 配置、.venv、WPF 编译输出
├── README.md                    # 项目说明文档（本文件）
│
├── run.bat                      # 命令行统一启动脚本，支持 4 种子命令
├── launcher.bat                 # 无控制台一键启动器，自动检测后端健康状态并编译前端
├── check.bat                    # 环境诊断脚本，检查 .NET SDK、uv、Python、API Key 等依赖
├── 启动.vbs                     # VBScript 双击启动器，完全无命令行窗口，自动启动后端+前端
│
├── backend/                     # Python 后端 (FastAPI)
│   ├── .env                     # 环境变量配置文件（API Key 等敏感信息，不入 git）
│   ├── .env.example             # 环境变量模板，列出所有可配置项及默认值
│   ├── .gitignore               # 后端专用忽略规则
│   ├── .python-version          # 指定 Python 版本：3.13
│   ├── pyproject.toml           # uv 项目清单：定义项目元数据、依赖、pytest 配置
│   ├── uv.lock                  # uv 依赖锁定文件，记录精确版本
│   │
│   ├── src/                     # 源代码
│   │   ├── __init__.py          # 空文件，标记 src 为 Python 包
│   │   ├── main.py              # FastAPI 应用入口
│   │   │
│   │   ├── agent/               # Agent 模块（预留，当前为空包）
│   │   │   └── __init__.py
│   │   │
│   │   ├── api/                 # API 层：路由、请求/响应模型、依赖注入
│   │   │   ├── __init__.py
│   │   │   ├── routes.py        # 所有 API 路由：树的 CRUD、聊天 SSE 流、配置、序列化、关闭
│   │   │   ├── schemas.py       # Pydantic 请求/响应模型（15 个类），含节点树的递归转换函数
│   │   │   └── dependencies.py  # FastAPI 依赖注入：提供 TreeManager / DeepSeekClient / FileService 单例
│   │   │
│   │   ├── core/                # 核心配置与异常体系
│   │   │   ├── __init__.py
│   │   │   ├── config.py        # Settings 类：从 .env 加载所有配置项，含服务端、DeepSeek API、运行时参数
│   │   │   └── errors.py        # 异常体系：TreeChatError 基类 + LLMError、ConfigError、TreeNotFoundError 等子类
│   │   │
│   │   ├── models/              # 领域模型（Pydantic）
│   │   │   ├── __init__.py
│   │   │   ├── chat_message.py  # ChatMessage：单条消息（role + content），支持 to_api_dict()
│   │   │   ├── chat_tree.py     # ChatTree：对话树，含树 ID、标题、根节点，支持 find_node、get_all_nodes
│   │   │   └── chat_tree_node.py# ChatTreeNode：树节点，含父子关系、get_full_context()、add_child()
│   │   │
│   │   └── services/            # 业务服务层
│   │       ├── __init__.py
│   │       ├── tree_manager.py  # TreeManager：对话树内存管理器，树的 CRUD + 节点的增删改 + AI 回复设置
│   │       ├── llm_client.py    # DeepSeekClient：DeepSeek V4 API 异步客户端，支持流式和非流式聊天
│   │       └── file_service.py  # FileService：.chat 文件 JSON 序列化/反序列化（版本号 1.0）
│   │
│   └── tests/                   # pytest 测试套件
│       ├── __init__.py
│       ├── conftest.py          # 测试配置与共享 fixtures：tree_manager、sample_tree
│       ├── test_tree_manager.py # 测试 TreeManager CRUD + 节点操作 + 上下文/分支/删除/计数（14 个用例）
│       └── test_file_service.py # 测试 FileService 序列化往返、嵌套子节点、自定义标题（4 个用例）
│
└── gui/                         # WPF 前端 (.NET 8)
    ├── TreeChat.csproj          # .NET 项目文件：WinExe、net8.0-windows、WPF、NuGet 依赖
    ├── App.xaml                 # WPF 应用入口：主题色、全局样式（Button/TextBox/ListBox 等）
    ├── App.xaml.cs              # App 类：启动时加载配置→启动 Python 后端→推送配置；退出时清理
    ├── AssemblyInfo.cs          # 程序集主题信息属性
    │
    ├── Commands/                # ICommand 实现（MVVM 命令绑定）
    │   ├── RelayCommand.cs      # 同步命令：Action<object?> + CanExecute，支持手动刷新
    │   └── AsyncRelayCommand.cs # 异步命令：Func<object?, Task> + 执行中锁定防止重复点击
    │
    ├── Models/                  # 领域模型 + DTO
    │   ├── ChatMessage.cs       # 聊天消息（不可变）：Role + Content
    │   ├── ChatMessageData.cs   # ChatMessage 的 JSON 序列化 DTO，支持模型互转
    │   ├── ChatTreeNode.cs      # 树节点：含父子引用、NodeID 自动分配、get_full_context()
    │   ├── ChatTreeNodeData.cs  # ChatTreeNode 的 JSON 序列化 DTO，支持递归转换
    │   ├── ChatTree.cs          # 对话树：含 RootNode、CurrentNode、TreeId、TreeTitle，实现 INotifyPropertyChanged
    │   ├── ChatTreeData.cs      # ChatTree 的 JSON 序列化 DTO（含 Version、CreatedTime）
    │   └── ApiModels.cs         # 16 个 API 通信 DTO：请求体、响应体、SSE 事件解析
    │
    ├── Services/                # 服务层
    │   ├── ApiConfig.cs         # 静态全局配置类：API Key、模型名、温度等，支持 Load/Save JSON 持久化
    │   ├── ApiConfigData.cs     # ApiConfig 的序列化 DTO
    │   ├── PythonBackendService.cs # Python 后端管理：启动/停止 uvicorn、健康检查、全部 API 调用封装、SSE 流
    │   ├── AIClient.cs          # OpenAI SDK 客户端封装（预留，当前实际使用 OpenAIChat）
    │   ├── OpenAIChat.cs        # OpenAI 兼容 API 调用：构建 HTTP 请求→发送→解析回复
    │   ├── ChatCompletionRequest.cs # Chat Completion 请求体 + OpenAIMessage DTO
    │   ├── AiCallResult.cs      # AI 调用结果：Success(content) 或 Fail(errorKey, detail, statusCode)
    │   ├── OpenApiErrorParser.cs # API 错误解析器：HTTP 状态码→友好的中文错误提示
    │   ├── FileService.cs       # 本地 .chat 文件读写：显示保存/打开对话框，调用 JsonSerializationService
    │   ├── IFileService.cs      # FileService 接口
    │   ├── JsonSerializationService.cs # ChatTree ↔ JSON 序列化/反序列化（Newtonsoft.Json）
    │   └── TreeLayoutService.cs # 树形布局算法：递归计算子树宽度→确定根节点 x/y→渲染位置
    │
    ├── ViewModels/              # MVVM ViewModel 层
    │   ├── BaseViewModel.cs     # MVVM 基类：INotifyPropertyChanged 实现 + SetProperty 辅助方法
    │   ├── MainWindowVM.cs      # 主窗口 VM：持有三个子 VM，串联 SelectedChatChanged→SelectedNodeChanged→ChatTreeChanged 事件链
    │   ├── ChatManagementPanelVM.cs # 对话管理面板 VM：创建/保存/加载/重命名对话，维护 ChatList 集合
    │   ├── TreeVisualizationVM.cs   # 树可视化 VM：设置根节点、选中节点、触发画布重绘、配置/重命名命令
    │   ├── TreeNodeVM.cs        # 树节点 VM：X/Y 坐标、DisplayContent、子树宽度、递归构建子节点 VM
    │   ├── ChatInformationVM.cs # 聊天信息 VM：显示选中节点的问答、发送消息（SSE 流式）、乐观 UI 更新
    │   ├── ConfigDialogVM.cs    # 配置对话框 VM：API Key/端点/模型/温度/TopP/TopK 编辑与校验
    │   ├── CreateChatDialogVM.cs# 创建对话对话框 VM：标题+系统提示词 输入与校验
    │   └── RenameDialogVM.cs    # 重命名对话框 VM：新名称输入与校验
    │
    └── Views/                   # XAML 视图 + code-behind
        ├── MainWindow.xaml      # 主窗口：三列布局（管理面板 | 树画布 | 聊天面板）
        ├── MainWindow.xaml.cs   # MainWindow code-behind：初始化 VM、处理拖放 .chat 文件
        ├── ChatManagementPanel.xaml    # 对话管理面板：新建/保存/加载/重命名按钮 + 对话列表
        ├── ChatManagementPanel.xaml.cs
        ├── TreeVisualizationView.xaml  # 树可视化：Canvas 画布、缩放/平移/点击选择/拖放、工具栏
        ├── TreeVisualizationView.xaml.cs # 渲染逻辑：递归画连线+节点边框、高亮选中、鼠标交互
        ├── ChatInformationView.xaml    # 聊天面板：选中节点的问答展示 + 消息输入框
        ├── ChatInformationView.xaml.cs
        ├── ConfigDialog.xaml           # 配置对话框：API Key、模型参数编辑
        ├── ConfigDialog.xaml.cs
        ├── CreateChatDialog.xaml       # 新建对话对话框：标题 + 系统提示词
        ├── CreateChatDialog.xaml.cs
        ├── RenameDialog.xaml           # 重命名对话框
        └── RenameDialog.xaml.cs
```

### 非代码文件功能说明

| 文件 | 类型 | 作用 |
|---|---|---|
| `.gitignore` | Git 配置 | 忽略 `__pycache__/`、`.env`、`.venv/`、`gui/bin/`、`gui/obj/` 等不应提交的文件 |
| `README.md` | 文档 | 项目说明、结构、快速开始、技术栈 |
| `run.bat` | 启动脚本 | 命令行入口，支持 `backend`/`gui`/`all`/`test` 四个子命令 |
| `launcher.bat` | 启动脚本 | 无控制台启动器，自动检测后端健康状态→自动编译前端→启动 |
| `check.bat` | 诊断脚本 | 检查 .NET SDK、uv、Python 环境、API Key 是否正确配置 |
| `启动.vbs` | 启动脚本 | VBScript 双击启动器，完全无命令行窗口，适合普通用户双击使用 |
| `backend/.env` | 配置文件 | DeepSeek API Key、服务端口等敏感/本地配置 |
| `backend/.env.example` | 配置模板 | 列出所有可配置环境变量及默认值，供新用户复制为 `.env` |
| `backend/.python-version` | 版本标记 | 声明项目使用 Python 3.13 |
| `backend/pyproject.toml` | 项目清单 | uv 包管理器配置：项目元数据、依赖声明、pytest 选项 |
| `backend/uv.lock` | 锁文件 | 所有依赖的精确版本锁定，保证可复现构建 |
| `gui/TreeChat.csproj` | 项目清单 | .NET 8 WPF 项目配置：目标框架、NuGet 包引用 |
| `gui/App.xaml` | WPF 资源 | 全局主题色（蓝色系）、隐式样式（Button/TextBox/ListBox）、卡片阴影 |
| `gui/AssemblyInfo.cs` | 程序集属性 | 声明 WPF 主题资源查找策略 |

### 后端代码文件详情

#### `backend/src/main.py` — FastAPI 应用入口
- 创建 `FastAPI` 实例（标题 "TreeChat Backend"）
- 配置 CORS 中间件（允许全部来源）
- 注册三个异常处理器：`TreeChatError`、`LLMError`、通用 `Exception`
- 注册 API 路由
- 提供 `main()` 函数作为 uvicorn 启动入口

#### `backend/src/core/config.py` — 应用配置
- **`Settings(BaseSettings)`**：从 `.env` 和环境变量加载配置
  - 服务端：`host`(127.0.0.1)、`port`(8800)
  - DeepSeek API：`api_key`、`api_base`、`model`、`temperature`(0.7)、`top_p`(0.8)、`top_k`(20)、`max_tokens`(800)、`timeout`(120s)
  - 运行时配置（可通过 PUT /api/v1/config 覆盖）：`model`、`temperature`、`top_p`、`top_k`、`max_tokens`
- 模块级单例 `settings = Settings()`

#### `backend/src/core/errors.py` — 异常体系
- **`TreeChatError(Exception)`**：应用级基类异常
- **`LLMError(TreeChatError)`**：LLM API 调用错误，携带 `key`、`message`、`detail`、`status_code`
- **`ConfigError`**、**`TreeNotFoundError`**、**`NodeNotFoundError`**：细分异常子类
- **`_HTTP_ERROR_MAP`**：HTTP 状态码(401/403/422/429/500/503) → 中文错误提示的映射表
- **`map_http_error()`**：将 `httpx.HTTPStatusError` 转为 `LLMError`，尝试从响应体提取详细错误信息

#### `backend/src/models/chat_message.py` — 聊天消息
- **`ChatMessage(BaseModel)`**：单条消息（不可变值对象）
  - 字段：`role`（system/user/assistant）、`content`
  - **`to_api_dict()`**：转为 OpenAI 兼容的 `{"role": ..., "content": ...}` 字典

#### `backend/src/models/chat_tree_node.py` — 聊天树节点
- **`ChatTreeNode(BaseModel)`**：树节点
  - 字段：`node_id`、`user_message: ChatMessage`、`reply_message: ChatMessage | None`、`name`、`children: list[ChatTreeNode]`
  - 私有字段 `_parent`：不参与序列化，仅用于遍历
  - **`parent`**(property)：返回父节点引用
  - **`set_parent()`**：设置父节点引用
  - **`get_full_context()`**：从根到当前节点，沿父链收集所有消息，返回完整对话上下文列表
  - **`add_child()`**：添加子节点并自动设置双向父子关系
  - **`remove_child()`**：按 node_id 移除子节点并解除父子关系

#### `backend/src/models/chat_tree.py` — 对话树
- **`ChatTree(BaseModel)`**：对话树
  - 字段：`tree_id`（12 位 hex UUID）、`title`、`root_node: ChatTreeNode`、`created_at`（ISO 时间戳）
  - **`create()`**(classmethod)：工厂方法，用可选系统提示词创建带有根节点的树
  - **`find_node()`**：递归按 node_id 查找节点
  - **`get_all_nodes()`**：DFS 遍历返回所有节点列表
  - **`node_count`**(property)：节点总数

#### `backend/src/api/schemas.py` — Pydantic 请求/响应模型
- 15 个 Pydantic 模型定义，涵盖：树的增删改查、节点消息、聊天请求、配置数据、错误详情、健康检查、序列化
- **`_node_to_data(node)`**：将内部 `ChatTreeNode` 递归转换为 API 响应的 `TreeNodeData`

#### `backend/src/api/routes.py` — API 路由
- 路由前缀 `/api/v1`
- **`GET /health`**：健康检查，返回 `{"status": "ok", "version": "0.1.0"}`
- **`POST /trees`**：创建新对话树
- **`GET /trees`**：列出所有对话树
- **`GET /trees/{tree_id}`**：获取单个树详情（含递归节点数据）
- **`DELETE /trees/{tree_id}`**：删除对话树
- **`PUT /trees/{tree_id}`**：重命名对话树
- **`POST /trees/{tree_id}/chat`**：SSE 流式端点，发送消息到指定父节点，实时返回 AI 回复
- **`PUT /trees/{tree_id}/nodes/{node_id}`**：重命名节点
- **`DELETE /trees/{tree_id}/nodes/{node_id}`**：删除节点及其子树
- **`GET /config`** / **`PUT /config`**：获取/更新运行时 LLM 参数
- **`GET /trees/{tree_id}/serialize`**：将树序列化为 JSON 字符串
- **`POST /trees/deserialize`**：从 JSON 反序列化导入树
- **`POST /shutdown`**：关闭 LLM 客户端并终止服务进程
- **异常处理器**：`treechat_error_handler`、`llm_error_handler`、`generic_error_handler`，统一错误 JSON 格式

#### `backend/src/api/dependencies.py` — 依赖注入
- **`get_tree_manager()`**：返回全局 `TreeManager` 单例
- **`get_llm_client()`**：返回全局 `DeepSeekClient` 单例
- **`get_file_service()`**：返回全局 `FileService` 单例

#### `backend/src/services/tree_manager.py` — 对话树管理服务
- **`_NextNodeId`**：线程安全全局节点 ID 计数器，从 2 开始（1 预留给根节点）
  - `next()`：原子地返回当前值并自增
  - `ensure_at_least(value)`：确保计数器至少为 value+1（反序列化后防止 ID 冲突）
- **`TreeManager`**：对话树内存管理器（单例）
  - **CRUD**：`create_tree()`、`get_tree()`、`list_trees()`、`delete_tree()`、`rename_tree()`、`add_tree()`
  - **节点操作**：`get_node()`、`add_child_node()`、`rename_node()`、`delete_node()`、`set_ai_reply()`
  - **序列化辅助**：`reset_node_id_counter()`

#### `backend/src/services/llm_client.py` — LLM API 客户端
- **`DeepSeekClient`**：异步 HTTP 客户端封装
  - `_get_client()`：懒加载初始化 `httpx.AsyncClient`（配置 base_url、timeout、Authorization 头）
  - `_build_payload()`：构建 API 请求体（top_k 仅在 UI 使用，不发送到 API）
  - `chat()`：非流式聊天，返回完整响应 `dict`
  - `chat_stream()`：流式聊天，`AsyncGenerator` 逐 token 产出，处理 SSE `data:` 行和 `[DONE]` 标记
  - `close()`：关闭 HTTP 客户端

#### `backend/src/services/file_service.py` — 文件序列化服务
- **`FileService`**：`.chat` 文件读写
  - 类常量 `CURRENT_VERSION = "1.0"`
  - `serialize(tree)`：将 `ChatTree` 转为 JSON 字符串（含 Version、TreeTitle、CreatedTime、RootNode）
  - `deserialize(json_str, title)`：从 JSON 重建 `ChatTree`，可覆盖标题
  - `_serialize_node()` / `_deserialize_node()`：递归处理节点数据

#### `backend/tests/conftest.py` — pytest 配置与 fixtures
- `tree_manager()` fixture：提供全新的 `TreeManager` 实例
- `sample_tree(tree_manager)` fixture：创建带系统提示词的测试树

#### `backend/tests/test_tree_manager.py` — TreeManager 测试（14 用例）
- **`TestTreeManager`**(6 用例)：创建树、列出树、获取树、树不存在、删除树、重命名树
- **`TestNodeOperations`**(6 用例)：添加子节点、获取完整上下文、分支上下文隔离、重命名节点、删除节点、禁止删除根节点
- **`TestNodeCount`**(2 用例)：初始计数为 1、添加节点后计数为 3

#### `backend/tests/test_file_service.py` — FileService 测试（4 用例）
- **`TestFileService`**：序列化往返一致性、保留嵌套子节点、空树序列化、加载时自定义标题

### 前端代码文件详情

#### `gui/Commands/` — MVVM 命令绑定

- **`RelayCommand`**：同步 ICommand 实现
  - 构造函数接收 `Action<object?>` 和可选的 `CanExecute` 条件
  - `OnCanExecuteChanged()`：手动触发 UI 刷新按钮可用性

- **`AsyncRelayCommand`**：异步 ICommand 实现
  - 构造函数接收 `Func<object?, Task>` 和可选的 `CanExecute` 条件
  - 执行期间设置 `_isExecuting = true` 防止重复点击
  - 执行前后自动调用 `OnCanExecuteChanged()` 刷新 UI 状态

#### `gui/Models/` — 领域模型 + DTO

- **`ChatMessage`**：不可变聊天消息（role + content）
- **`ChatMessageData`**：用于 JSON 序列化的可读写版本，支持 `ToChatMessage()` 互转
- **`ChatTreeNode`**：树节点，含 `ParentNode`、`ChildNodes`、`UserMessage`、`ReplyMessage`、`NodeID`（静态自增）
  - `GetFullContext()`：沿父链收集完整对话上下文
  - `AddChildNode()`：创建子节点并建立双向引用
  - `SetAiReply()`：设置 AI 回复
  - `ResetNextNodeId()` / `GetCurrentNextNodeId()`：管理全局 NodeID 计数器
- **`ChatTreeNodeData`**：树节点的序列化 DTO，支持 `ToChatTreeNode()` 递归转换
- **`ChatTree`**：对话树，实现 `INotifyPropertyChanged`
  - `RootNode`、`CurrentNode`、`TreeId`、`TreeTitle`（可通知属性）
  - `SetRootNode()`、`FindNodeById()`：根节点设置与节点查找
- **`ChatTreeData`**：对话树的序列化 DTO（含 Version、CreatedTime）
- **`ApiModels.cs`**(16 个 DTO)：`CreateTreeRequest`、`TreeSummary`、`TreeDetailResponse`、`ChatRequest`、`ChatConfigData`、`ApiSuccessResponse`、`SseCreatedEvent`、`SseDeltaEvent`、`SseDoneEvent`、`SseErrorEvent`、`SerializeResponse`、`DeserializeRequest` 等

#### `gui/Services/` — 服务层

- **`ApiConfig`**(静态类)：全局配置管理器
  - 静态字段：`ApiKey`、`PythonBackendUrl`、`ModelName`、`Temperature`(0.7)、`TopP`(0.8)、`TopK`(20)、`MaxTokens`(800)
  - `LoadFromFile()`：从 `%AppData%/TreeChat/api_config.json` 加载配置
  - `SaveToFile()`：持久化当前配置到 JSON

- **`PythonBackendService`**：Python 后端的 .NET 管理客户端（实现 `IDisposable`）
  - `StartAsync()`：启动 `uv run uvicorn` 进程，轮询健康检查直到就绪
  - `StopAsync()`：发送 shutdown 请求，必要时 kill 进程
  - `HealthCheckAsync()`：GET `/api/v1/health`
  - Tree CRUD：`CreateTreeAsync`、`ListTreesAsync`、`GetTreeAsync`、`DeleteTreeAsync`、`RenameTreeAsync`
  - `ChatStreamAsync()`：POST SSE 流式聊天，返回 `IAsyncEnumerable<SseEvent>`
  - Node：`RenameNodeAsync`、`DeleteNodeAsync`
  - Config/File：`GetConfigAsync`、`PushConfigAsync`、`SerializeTreeAsync`、`DeserializeTreeAsync`
  - 附带 `SseEvent` 类：`EventType` + `Data`

- **`OpenAIChat`**：OpenAI 兼容 API 调用（使用原始 `HttpClient`）
  - `CallAiApi()`：构建请求体→POST 到 API 端点→解析 `choices[0].message.content`
  - `TryParseAssistantContent()`：从 JSON 响应提取 AI 回复文本

- **`AiCallResult`**(sealed)：AI 调用结果封装
  - 静态工厂 `Success(content)` 和 `Fail(errorKey, detail, statusCode)`
  - 只读属性：`IsSuccess`、`Content`、`ErrorKey`、`ErrorDetail`、`StatusCode`

- **`OpenApiErrorParser`**(静态类)：HTTP 状态码→中文错误提示映射

- **`FileService`**：本地文件服务（实现 `IFileService`），显示 Windows 保存/打开对话框进行 .chat 文件读写

- **`JsonSerializationService`**：Newtonsoft.Json 序列化/反序列化，处理 ChatTree ↔ ChatTreeData ↔ JSON

- **`TreeLayoutService`**(静态类)：树形布局算法
  - `LayoutTree(rootNode)`：全量布局（从根开始递归计算位置）
  - `UpdateLayoutTree(updateNode)`：增量更新布局
  - 核心算法：逐层向下为每个节点的子树分配水平空间，父节点水平居中于子节点，固定垂直间距

- **`AIClient`**：OpenAI SDK `ChatClient` 封装（预留，当前实际网络调用走 `OpenAIChat`）

- **`ChatCompletionRequest`** + **`OpenAIMessage`**：Chat Completion API 请求体 DTO

#### `gui/ViewModels/` — ViewModel 层

- **`BaseViewModel`**：MVVM 基类，`INotifyPropertyChanged` 实现 + `SetProperty<T>(ref field, value)` 辅助方法

- **`MainWindowVM`**：主窗口 ViewModel
  - 持有三个子 VM：`ChatManagementPanelVM`、`TreeVisualizationVM`、`ChatInformationVM`
  - 串联事件链：选中对话变化→更新树可视化→选中节点变化→更新聊天面板

- **`ChatManagementPanelVM`**：对话管理面板
  - `ChatList`（ObservableCollection）绑定到左侧列表
  - 命令：`CreateNewChat`（弹出对话框→创建树→加入列表）、`SaveChat`（优先 Python 后端序列化，回退本地）、`LoadChat`（优先后端反序列化，回退本地）、`RenameChat`（弹出对话框→更新标题）

- **`TreeVisualizationVM`**：树可视化
  - `RootNode`、`SelectedNode`、`CanvasPropertyChanged` 事件
  - `ShowConfigCommand`：弹出配置对话框，保存到 `ApiConfig` 并推送后端
  - `RenameNodeCommand`：弹出重命名对话框，同步后端，重新布局

- **`TreeNodeVM`**：树节点 ViewModel
  - `X`、`Y`（画布坐标）、`DisplayContent`（Name 或 NodeID）、`SubtreeWidth`
  - 递归构造函数：自动为 Model 的每个 ChildNode 创建子 TreeNodeVM
  - `AddChild()`、`RemoveChild()`：同步操作 Model 和 VM

- **`ChatInformationVM`**：聊天面板
  - `UserMessage`、`AIReply`（选中节点的问答展示，只读）
  - `InputMessage`（双向绑定输入框）
  - `SendMessage`（AsyncRelayCommand）：乐观 UI（先创建节点）→ SSE 流式接收→逐 token 更新→完成/错误处理→清理
  - `GetUserFriendlyErrorPrompt()`：将错误 Key 映射为中文提示

- **`ConfigDialogVM`**：配置对话框，校验 Temperature(0-2)、TopP(0-1)、TopK(0-40)，显示默认值提示

- **`CreateChatDialogVM`**：新建对话对话框，校验标题非空

- **`RenameDialogVM`**：重命名对话框，校验新名称非空

#### `gui/Views/` — 视图层 XAML + code-behind

- **`MainWindow`**：三列布局窗口，code-behind 处理初始文件加载和拖放 .chat 文件
- **`ChatManagementPanel`**：左侧面板，按钮 + ListBox 对话列表
- **`TreeVisualizationView`**：中央 Canvas 画布
  - 交互：Ctrl+滚轮缩放、鼠标拖拽平移、点击选中节点
  - 渲染：递归绘制连线（Line）→ 节点边框（Border+TextBlock）→ 高亮选中节点（蓝色边框）
  - 支持拖放 .chat 文件到画布上打开
- **`ChatInformationView`**：右侧聊天面板，选中节点问答展示 + 多行消息输入 + 发送按钮
- **`ConfigDialog`**：配置窗口，API Key/模型参数编辑区
- **`CreateChatDialog`**：新建对话窗口，标题 + 系统提示词输入
- **`RenameDialog`**：重命名窗口，文本输入框

## 快速开始

### 1. 配置 DeepSeek API Key

编辑 `backend/.env`:
```
DEEPSEEK_API_KEY=sk-your-actual-api-key
```

### 2. 启动

```bash
# 仅启动 Python 后端
run.bat

# 构建并启动 WPF 前端
run.bat gui

# 同时启动后端 + 前端
run.bat all

# 运行 Python 测试
run.bat test
```

### 3. 手动启动

```bash
# Python 后端
cd backend
uv run uvicorn src.main:app --host 127.0.0.1 --port 8800

# WPF 前端
cd gui
dotnet build && start bin/Debug/net8.0-windows/TreeChat.exe
```

## 技术栈

| 层 | 技术 |
|---|---|
| 后端 | Python 3.11+, FastAPI, httpx, Pydantic |
| 前端 | C# .NET 8, WPF, MVVM |
| AI | DeepSeek V4 API (OpenAI 兼容) |
| 通信 | HTTP REST + SSE 流式 |
| 包管理 | uv (Python), NuGet (.NET) |
