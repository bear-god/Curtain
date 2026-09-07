// <copyright file="IUIResourceLoader.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Abstraction;

using System.Threading;
using System.Threading.Tasks;
using Curtain.Core;

/// <summary>
/// UI Prefab 加载抽象。
/// 实现负责按路径加载 Prefab、实例化并取得 TView 组件，
/// 返回的视图契约上为"未激活"状态，由 UIManager 在绑定后激活。
/// 由资源层（如包装 YooAsset 的宿主实现）提供，核心程序集不依赖具体资源系统。
/// </summary>
public interface IUIResourceLoader
{
    /// <summary>异步加载 UI Prefab 并实例化，获取 <typeparamref name="TView"/> 组件。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <param name="prefabPath">Prefab 资源路径。</param>
    /// <param name="parent">父容器。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实例化后的视图（未激活）。</returns>
    ValueTask<TView> InstantiateAsync<TView>(
        string prefabPath,
        IViewContainer parent,
        CancellationToken cancellationToken = default)
        where TView : IView;

    /// <summary>释放已实例化的 UI 视图。</summary>
    /// <param name="view">要释放的视图实例。</param>
    void Release(IView view);
}
