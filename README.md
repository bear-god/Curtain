# Curtain

一个游戏 UI 管理框架，提供页面与对话框的统一打开、关闭和层级管理能力。核心引擎无关，当前提供 Unity 桥接（Curtain.Unity），后续将支持 Godot 等其他引擎。

## 特点

- **页面管理**：支持主界面、普通页面、顶层页面，以及多开计数。
- **对话框**：支持带返回值，可异步等待用户操作结果；父界面关闭时自动级联关闭子对话框。
- **参数注入**：打开页面或对话框时可传入参数，类型安全。
- **层级控制**：内置 Main、Stack、Effect、Top 四个层级，可一键关闭普通页面（保留主界面与顶层）。
- **生命周期清晰**：View 通过 `OnInitialize` / `OnHideAsync` 处理初始化与关闭收尾，框架负责调用时机。
- **资源自动释放**：View 通过 `AddDisposable` 注册的订阅，在销毁时自动释放。
- **核心引擎无关**：核心逻辑不依赖任何引擎类型，便于独立测试；Unity 桥接由 Curtain.Unity 提供，后续可扩展 Godot 等引擎桥接。

## 快速开始

```csharp
// 定义页面
[UI("UI/Page/ShopPage")]
public class ShopPageView : ViewBase
{
    protected override void OnInitialize()
    {
        // 初始化：缓存控件、创建数据、播放入场动画
    }
}
```

```csharp
// 打开页面
await uiManager.OpenMainAsync<ShopPageView>();
await uiManager.OpenPageAsync<ShopPageView>(UILayer.Stack);

// 带参数打开
await uiManager.OpenPageAsync<ShopPageView, ShopParam>(param);

// 关闭页面
UIHandle handle = await uiManager.OpenMainAsync<ShopPageView>();
handle.Dispose();

// 关闭所有普通页面（主界面和顶层保留）
uiManager.CloseAll();
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

## 层级说明

| 层级 | 用途 | CloseAll 行为 |
|------|------|--------------|
| `Main` | 主界面，打开新的会替换旧的 | 保留 |
| `Stack` | 普通页面和对话框 | 关闭 |
| `Effect` | 特效（飞金币、飘字等） | 保留 |
| `Top` | 新手引导、公告、强制更新 | 保留 |

## View 生命周期

- `OnInitialize()`：打开时调用一次，做初始化。
- `OnHideAsync()`：关闭前调用，播放出场动画或清理，框架会等待完成。
- `AddDisposable(...)`：注册订阅，View 销毁时自动释放。
