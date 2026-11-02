// <copyright file="IViewPostProcessor.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Abstraction;

using Curtain.Core;

/// <summary>
/// 视图打开链装饰接缝：在视图实例化、参数与结果句柄注入之后、<see cref="IView.Initialize"/> 之前调用一次，
/// 供宿主把应用侧依赖（视图数据解析器、界面服务、视图自身句柄等）注入视图。
/// 抛出异常按打开失败处理：摘除注册、执行 OpenFailed 生命周期并释放实例。
/// </summary>
public interface IViewPostProcessor
{
    /// <summary>处理刚实例化的视图。</summary>
    /// <param name="view">本次实例化的视图。</param>
    /// <param name="handle">本次打开的句柄；此时尚未注册，<see cref="UIHandle.IsOpen"/> 为 false。</param>
    /// <param name="layer">视图所在层级。</param>
    void Process(IView view, UIHandle handle, UILayer layer);
}
