// <copyright file="UILayer.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Core;

/// <summary>UI 层级（数值越大渲染越靠上）</summary>
public enum UILayer
{
    /// <summary>主界面：CloseAll 不关闭，切换时替换</summary>
    Main = 0,

    /// <summary>堆叠层：大部分页面和对话框，CloseAll 关闭此层</summary>
    Stack = 10,

    /// <summary>动效层：飞金币、飘字等纯视觉特效</summary>
    Effect = 50,

    /// <summary>最高层：新手引导、公告、强制更新等，CloseAll 不关闭</summary>
    Top = 100,
}