// <copyright file="IView.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Abstraction;

using System.Threading.Tasks;

/// <summary>
/// View 抽象。框架通过此接口操作视图，不依赖任何 Unity 类型。
/// 由 Curtain.Unity 的 ViewBase（MonoBehaviour）实现。
/// 框架不感知 ViewModel：VM 的创建与生命周期由 View 自身（或应用层）负责。
/// 框架不管理打开/关闭动画：显示时 View 自行决定是否播放入场动画及后续操作，
/// 框架只在关闭前调用 <see cref="HideAsync"/> 并等待其完成，保证出场动画与清理有收尾机会。
/// </summary>
public interface IView
{
    /// <summary>设置视图是否可见。</summary>
    /// <param name="active">true 表示可见。</param>
    void SetActive(bool active);

    /// <summary>由 UIManager 在视图实例化、注入参数/结果句柄后调用一次，做视图自身初始化。</summary>
    void Initialize();

    /// <summary>框架内部：关闭前调用并等待，View 自行决定是否播出场动画、动画后如何清理。</summary>
    /// <returns>表示异步操作的 ValueTask。</returns>
    ValueTask HideAsync();
}
