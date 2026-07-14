# Plan: 树形图缩放功能 — 节点同步缩放 + 缩放限制

## Context

用户发现在 TreeVisualizationView 中缩放时（Ctrl+滚轮），节点大小不变而连线位置变化，导致线段与节点边缘错位。原因是 NodifyEditor 的 Decorators 层不应用缩放变换（DecoratorContainer 视觉尺寸固定），而连线层使用缩放后的图空间坐标。需要让节点视觉尺寸随缩放同步变化，并设置合理的缩放范围防止节点过小无法点击或过大溢出。

## 修改方案

### 1. TreeNodeVM.cs — 修正 WIDTH/HEIGHT 常量

将常量与视觉模板最小尺寸对齐：`WIDTH: 40 → 52`，`HEIGHT: 30 → 32`。

影响：
- ConnectionVM.UpdatePoints() — 端点计算自动对齐视觉中心
- OnTreeRendered() — 包围盒计算使用正确尺寸

### 2. TreeVisualizationView.xaml — 核心缩放修改

**a) 设置缩放范围：**
```xml
MinViewportZoom="0.2" MaxViewportZoom="3.0"
```

**b) DecoratorTemplate 添加 ScaleTransform：**
在 Border 上添加 `RenderTransform`（ScaleTransform），绑定到 NodifyEditor 的 ViewportZoom（通过 RelativeSource 祖先查找），`RenderTransformOrigin="0,0"`。

**c) 添加缩放工具栏控件：**
工具栏右侧新增：缩小按钮(−)、百分比显示、放大按钮(+)、重置按钮(1:1)。

**d) 添加 `Loaded` 事件：**
`UserControl` 上挂载 `Loaded="TreeVisualizationView_Loaded"`。

**e) 添加 `ViewportUpdated` 事件：**
NodifyEditor 上挂载 `ViewportUpdated="Editor_ViewportUpdated"` 用于实时更新百分比显示。

### 3. TreeVisualizationView.xaml.cs — 缩放逻辑

**新增方法：**
- `TreeVisualizationView_Loaded()` — 确保缩放限制、初始化百分比显示
- `UpdateZoomDisplay()` — 更新百分比文本
- `Editor_ViewportUpdated()` — ViewportUpdated 事件处理器
- `ZoomIn_Click()` / `ZoomOut_Click()` — 步进 1.3x
- `ZoomReset_Click()` — 重置为 1.0

**修复拖拽平移：**
`Editor_PreviewMouseMove` 中 delta 除以 `ViewportZoom`，因为 `GetPosition(editor)` 返回屏幕像素，而 `ViewportLocation` 是图空间坐标：
```csharp
editor.ViewportLocation = new Point(
    _panStartViewport.X - delta.X / zoom,
    _panStartViewport.Y - delta.Y / zoom);
```

**重置缩放：**
`OnTreeRendered()` 中添加 `editor.ViewportZoom = 1.0`，确保初始视图一致。

## 修改文件列表

| 文件 | 修改内容 |
|------|---------|
| `gui/ViewModels/TreeNodeVM.cs` | WIDTH: 40→52, HEIGHT: 30→32 |
| `gui/Views/TreeVisualizationView.xaml` | Min/MaxViewportZoom, DecoratorTemplate + RenderTransform/ScaleTransform, 工具栏缩放按钮, Loaded/ViewportUpdated 事件 |
| `gui/Views/TreeVisualizationView.xaml.cs` | 缩放按钮处理, ViewportUpdated, Loaded, 修复平移除以缩放比, OnTreeRendered 重置缩放 |

## 不变文件

- `ConnectionVM.cs` — 自动继承 TreeNodeVM.WIDTH/HEIGHT
- `TreeVisualizationVM.cs` — 缩放是纯视觉行为，不进 VM
- `TreeLayoutService.cs` — 布局位置值独立于 WIDTH 常量

## 验证方法

1. 加载 .chat 文件 → 树以 100% 居中显示，节点与连线对齐
2. 点击放大按钮(+) → 节点变大，连线端点始终对齐节点边缘
3. 点击缩小按钮(−) → 节点变小，仍可点击
4. Ctrl+滚轮缩放 → 节点同步缩放，比例显示更新
5. 缩放后拖拽平移 → 平移速度正常（不受缩放影响）
6. 点击 1:1 → 回到 100%
7. 缩放超出 0.2~3.0 范围 → 被限制
