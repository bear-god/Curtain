using System;
using NUnit.Framework;
using UIFramework.Core;

namespace UIFramework.Tests;

/// <summary>简单值类型与异常契约测试。</summary>
[TestFixture]
public class CoreTypesTests
{
    /// <summary>UIAttribute 保存 Prefab 路径。</summary>
    [Test]
    public void UIAttribute_StoresPrefabPath()
    {
        var attr = new UIAttribute("UI/Page/Shop");
        Assert.That(attr.PrefabPath, Is.EqualTo("UI/Page/Shop"));
    }

    /// <summary>UIAttribute 空路径抛异常。</summary>
    [Test]
    public void UIAttribute_EmptyPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new UIAttribute(string.Empty));
        Assert.Throws<ArgumentException>(() => new UIAttribute(null));
    }

    /// <summary>UIOpenException 保存消息。</summary>
    [Test]
    public void UIOpenException_MessagePreserved()
    {
        var ex = new UIOpenException("boom");
        Assert.That(ex.Message, Is.EqualTo("boom"));
    }

    /// <summary>UIOpenException 保存内部异常。</summary>
    [Test]
    public void UIOpenException_InnerExceptionPreserved()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new UIOpenException("boom", inner);
        Assert.That(ex.InnerException, Is.SameAs(inner));
    }
}