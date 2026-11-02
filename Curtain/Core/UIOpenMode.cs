// <copyright file="UIOpenMode.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Core;

/// <summary>
/// 堆叠项（<see cref="UILayer.Stack"/> 层页面与对话框）的打开模式，决定它是否遮蔽下方视图。
/// </summary>
public enum UIOpenMode
{
    /// <summary>
    /// 独占：成为当前激活项，隐藏其下方所有堆叠项以及 Main 界面。
    /// </summary>
    Exclusive = 0,

    /// <summary>
    /// 叠加：显示在最近一个独占项之上，不隐藏其下方的任何视图。
    /// </summary>
    Overlay = 1,
}
