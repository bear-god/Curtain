# Curtain

一个游戏 UI 管理框架，提供页面与对话框的统一打开、关闭、层级与激活管理能力。核心引擎无关，由宿主提供资源加载、根节点、日志与视图实现。

## 安装

```bash
dotnet add package Curtain
```

## 特点

- **页面管理**：支持主界面、普通页面、顶层页面，以及多开计数。
- **有序堆叠**：Stack 层维护打开顺序，独占项（`UIOpenMode.Exclusive`）隐藏其下方视图与 Main，叠加项（`UIOpenMode.Overlay`）不遮蔽。
- **对话框**：支持带返回值，可异步等待用户操作结果；父界面关闭时自动级联关闭子对话框。
- **关闭语义**：`CloseTopAsync` 关闭最上方独立界面及其后代；`CloseAsync` 关闭指定界面；`BackToMainAsync` 清空 Stack 层并保留 Main 与 Top。
- **参数注入**：打开页面或对话框时可传入参数，类型安全。
- **生命周期清晰**：框架依次调用 `BeforeClose`、`BeforeCloseAsync`，随后由资源加载器统一释放实例。
- **命令队列**：所有改变导航状态的操作严格 FIFO 串行执行。
- **核心引擎无关**：核心逻辑不依赖任何引擎类型，便于独立测试；引擎桥接由宿主实现。

## 快速开始

```csharp
// 定义页面
[UI("UI/Page/ShopPage")]
public class ShopPageView : ViewBase
{
    public override UIOpenMode Mode => UIOpenMode.Exclusive;

    protected override void OnInitialize()
    {
        // 初始化：缓存控件、创建数据、播放入场动画
    }

    protected override ValueTask OnBeforeCloseAsync(CloseReason reason, CancellationToken cancellationToken)
    {
        // 关闭收尾：出场动画、注销订阅等
        return default;
    }
}
```

```csharp
// 打开 Main（默认拒绝重复打开）
UIHandle main = await uiManager.OpenMainAsync<HomeView>();

// 显式替换旧 Main（先打开新 Main，成功后再关闭旧 Main）
await uiManager.OpenMainAsync<HomeView>(MainOpenMode.Replace);

// 打开 Stack / Top 页面；mode 为 null 时使用 View.Mode
UIHandle page = await uiManager.OpenPageAsync<ShopPageView>(UILayer.Stack);
await uiManager.OpenPageAsync<ShopPageView, ShopParam>(param, UILayer.Stack, UIOpenMode.Overlay);

// 关闭
await uiManager.CloseAsync(page);
await uiManager.CloseTopAsync();
await uiManager.BackToMainAsync();

// 句柄：IsOpen 在摘除时变为 false；关闭开始后访问 View 会抛出 InvalidOperationException
page.Dispose(); // 尾部排队 Normal 关闭，不等待
```

## 对话框

```csharp
// 对话框 View：接收结果句柄，用户操作后返回结果
[UI("UI/Dialog/ConfirmDialog")]
public class ConfirmDialogView : ViewBase, IViewWithResult
{
    private UIHandle _resultHandle;

    public void SetResultHandle(UIHandle handle)
    {
        _resultHandle = handle;
    }

    public void OnConfirm()
    {
        ((UIHandle<bool>)_resultHandle).Complete(true);
    }
}
```

```csharp
// 打开并等待结果
UIHandle<bool> dialog = await uiManager.OpenDialogAsync<ConfirmDialogView, bool>(parentHandle);
UIResult<bool> result = await dialog.GetResult();
if (result.IsCompleted)
{
    // 用户确认，result.Value 为返回值
}
```

## 层级与关闭行为

| 层级 | 用途 | 关闭行为 |
|------|------|----------|
| `Main` | 主界面，同一时刻最多一个 | `OpenMainAsync` 默认拒绝重复打开，`MainOpenMode.Replace` 显式替换；只通过句柄关闭 |
| `Stack` | 普通页面和对话框，有序 | `CloseTopAsync` 关闭最上方独立界面及其后代；`BackToMainAsync` 全部关闭 |
| `Effect` | 特效（飞金币、飘字等） | 保留 |
| `Top` | 新手引导、公告、强制更新 | 保留 |

## 激活规则

- Stack 层最后一个 `Exclusive` 及其上方的视图可见，其下方全部隐藏。
- Stack 层没有 `Exclusive` 时，所有堆叠项可见。
- Main 仅在 Stack 层没有 `Exclusive` 时可见。
- 关闭中的视图保持原有激活状态，直到资源加载器释放实例。

## View 生命周期

- `Mode`：Stack 层打开模式，默认由各 View 实现决定；打开 API 显式传入时优先。
- `Initialize()`：实例化、注入参数/结果句柄并注册后调用一次。
- `BeforeClose(reason)`：关闭前同步钩子；抛出异常只记录日志。
- `BeforeCloseAsync(reason, cancellationToken)`：关闭前异步钩子，框架等待完成后才调用 `Release`；抛出异常只记录日志。
- 视图销毁只由 `IUIResourceLoader.Release` 负责，框架不额外调用销毁方法。

## 日志

框架通过 `Curtain.Abstraction.ILogger` 输出分级日志，由宿主实现后注入；不传时使用 `Curtain.Core.NullLogger.Instance`，不做任何输出。

```csharp
// 第三个构造参数可选，类型为 Curtain.Abstraction.ILogger
UIManager uiManager = new UIManager(loader, root, new MyLogger());
```

| 级别 | 触发时机 |
|------|----------|
| `LogDebug` | 打开界面、加载 Prefab、播放特效 |
| `LogInfo` | 界面打开完成、界面关闭 |
| `LogWarning` | `BeforeClose` / `BeforeCloseAsync` / `SetActive` 失败 |
| `LogError` | 打开失败、`Release` 失败 |

`LogWarning` 与 `LogError` 带可选的异常参数。宿主实现抛异常时框架会静默跳过，不打断界面生命周期。
