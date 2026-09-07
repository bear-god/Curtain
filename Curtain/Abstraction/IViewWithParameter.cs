// <copyright file="IViewWithParameter.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Abstraction;

/// <summary>
/// 可接收打开参数的 View 接口。
/// 实现了此接口的 View 通过 <see cref="SetParameter"/> 接收参数，走 UIManager 的带参打开链；
/// 未实现此接口的 View 走无参打开链，编译期即可保证类型安全。
/// </summary>
/// <typeparam name="TParam">参数类型。</typeparam>
public interface IViewWithParameter<TParam>
{
    /// <summary>接收打开时传入的参数，在 <see cref="IView.Initialize"/> 之前调用。</summary>
    /// <param name="parameter">页面或对话框参数。</param>
    void SetParameter(TParam parameter);
}
