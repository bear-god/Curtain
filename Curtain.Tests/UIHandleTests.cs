namespace Curtain.Tests;

using NUnit.Framework;
using Curtain.Core;

/// <summary>
/// <see cref="UIHandle{TResult}"/> 对话框结果语义测试：Complete / Dismiss 的幂等性与句柄有效性。
/// 通过 InternalsVisibleTo 直接构造句柄并调用框架内部 Dismiss。
/// </summary>
[TestFixture]
public class UIHandleTests
{
    /// <summary>Complete 设置已完成结果。</summary>
    [Test]
    public void Complete_SetsCompletedResult()
    {
        var handle = new UIHandle<int>(
            1,
            () =>
            {
            });
        handle.Complete(42);

        var result = handle.GetResult().GetAwaiter().GetResult();
        Assert.That(result.IsCompleted, Is.True);
        Assert.That(result.Value, Is.EqualTo(42));
    }

    /// <summary>Dismiss 设置未完成（取消）结果。</summary>
    [Test]
    public void Dismiss_SetsCancelledResult()
    {
        var handle = new UIHandle<int>(
            1,
            () =>
            {
            });
        handle.Dismiss();

        var result = handle.GetResult().GetAwaiter().GetResult();
        Assert.That(result.IsCompleted, Is.False);
    }

    /// <summary>多次 Complete 只有第一次生效。</summary>
    [Test]
    public void Complete_Twice_FirstWins()
    {
        var handle = new UIHandle<int>(
            1,
            () =>
            {
            });
        handle.Complete(1);
        handle.Complete(2);

        Assert.That(handle.GetResult().GetAwaiter().GetResult().Value, Is.EqualTo(1));
    }

    /// <summary>先 Dismiss 再 Complete，结果保持取消态。</summary>
    [Test]
    public void Dismiss_ThenComplete_KeepsCancelled()
    {
        var handle = new UIHandle<int>(
            1,
            () =>
            {
            });
        handle.Dismiss();
        handle.Complete(42);

        Assert.That(handle.GetResult().GetAwaiter().GetResult().IsCompleted, Is.False);
    }

    /// <summary>Dispose 幂等，关闭回调只触发一次。</summary>
    [Test]
    public void Dispose_IsIdempotent_AndInvokesOnCloseOnce()
    {
        var closeCount = 0;
        var handle = new UIHandle<int>(1, () => closeCount++);

        Assert.That(handle.IsValid, Is.True);
        handle.Dispose();
        handle.Dispose();

        Assert.That(handle.IsValid, Is.False);
        Assert.That(closeCount, Is.EqualTo(1));
    }
}
