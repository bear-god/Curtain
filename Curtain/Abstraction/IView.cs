// <copyright file="IView.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Abstraction;

using System.Threading;
using System.Threading.Tasks;
using Curtain.Core;

/// <summary>
/// View 抽象。框架通过此接口操作视图，不依赖任何引擎类型。
/// 框架不感知 ViewModel：VM 的创建与生命周期由 View 自身（或应用层）负责。
/// 框架不管理打开/关闭动画：显示时 View 自行决定是否播放入场动画及后续操作；
/// 关闭时框架依次调用 <see cref="BeforeClose"/> 与 <see cref="BeforeCloseAsync"/>，
/// 两个钩子都返回后由 <see cref="IUIResourceLoader.Release"/> 统一释放实例。
/// </summary>
public interface IView
{
    /// <summary>
    /// 打开模式，仅对 <see cref="UILayer.Stack"/> 层生效；
    /// 打开时显式传入的模式优先于此属性。
    /// </summary>
    UIOpenMode Mode { get; }

    /// <summary>设置视图是否可见。</summary>
    /// <param name="active">true 表示可见。</param>
    void SetActive(bool active);

    /// <summary>由 UIManager 在视图实例化、注入参数/结果句柄并注册后调用一次，做视图自身初始化。</summary>
    void Initialize();

    /// <summary>
    /// 关闭前同步调用的钩子，用于停止动画、注销事件等同步清理；抛出异常只记录日志，不阻断关闭。
    /// </summary>
    /// <param name="reason">关闭原因。</param>
    void BeforeClose(CloseReason reason);

    /// <summary>
    /// 关闭前异步调用的钩子，在 <see cref="BeforeClose"/> 之后调用并等待完成。
    /// 抛出异常只记录日志；无论成功与否，<see cref="IUIResourceLoader.Release"/> 都会在结束后执行。
    /// </summary>
    /// <param name="reason">关闭原因。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示异步清理完成的 ValueTask。</returns>
    ValueTask BeforeCloseAsync(CloseReason reason, CancellationToken cancellationToken = default);
}
