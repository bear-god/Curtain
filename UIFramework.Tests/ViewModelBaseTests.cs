using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UIFramework.Core;

namespace UIFramework.Tests;

/// <summary>
/// <see cref="ViewModelBase"/> 生命周期语义测试：初始化、显示/隐藏、释放顺序与幂等性。
/// 通过 InternalsVisibleTo 直接调用框架内部方法模拟框架驱动。
/// </summary>
[TestFixture]
public class ViewModelBaseTests
{
    /// <summary>Dispose 幂等：OnDispose 只调用一次。</summary>
    [Test]
    public void Dispose_IsIdempotent_AndCallsOnDisposeOnce()
    {
        var vm = new RecordingVm();
        vm.Initialize();
        vm.Dispose();
        vm.Dispose();

        Assert.That(vm.DisposeCount, Is.EqualTo(1));
    }

    /// <summary>OnBack 默认返回 true。</summary>
    [Test]
    public void OnBack_ReturnsTrueByDefault()
    {
        var vm = new RecordingVm();
        Assert.That(vm.OnBack(), Is.True);
    }

    /// <summary>Initialize 调用 OnInitialize 并注入生命周期回收容器。</summary>
    [Test]
    public void Initialize_InvokesOnInitialize_WithDisposables()
    {
        var vm = new RecordingVm();
        vm.Initialize();

        Assert.That(vm.InitCount, Is.EqualTo(1));
        Assert.That(vm.InitDisposables, Is.Not.Null);
        Assert.That(vm.InitDisposables.IsDisposed, Is.False);
    }

    /// <summary>NotifyShowAsync 调用 OnShow。</summary>
    [Test]
    public void NotifyShowAsync_InvokesOnShow()
    {
        var vm = new RecordingVm();
        vm.Initialize();
        vm.NotifyShowAsync().GetAwaiter().GetResult();

        Assert.That(vm.ShowCount, Is.EqualTo(1));
    }

    /// <summary>NotifyHideAsync 调用 OnHide。</summary>
    [Test]
    public void NotifyHideAsync_InvokesOnHide()
    {
        var vm = new RecordingVm();
        vm.Initialize();
        vm.NotifyHideAsync().GetAwaiter().GetResult();

        Assert.That(vm.HideCount, Is.EqualTo(1));
    }

    /// <summary>Dispose 释放 Initialize 注入的回收容器。</summary>
    [Test]
    public void Dispose_DisposesInitDisposables()
    {
        var vm = new RecordingVm();
        vm.Initialize();
        vm.Dispose();

        Assert.That(vm.InitDisposables.IsDisposed, Is.True);
    }

    private sealed class RecordingVm : ViewModelBase
    {
        public int InitCount;
        public int ShowCount;
        public int HideCount;
        public int DisposeCount;
        public CompositeDisposable InitDisposables;

        protected override void OnInitialize(CompositeDisposable disposables)
        {
            InitCount++;
            InitDisposables = disposables;
        }

        protected override UniTask OnShowAsync(CompositeDisposable clearWhenHide)
        {
            ShowCount++;
            return UniTask.CompletedTask;
        }

        protected override UniTask OnHideAsync()
        {
            HideCount++;
            return UniTask.CompletedTask;
        }

        protected override void OnDispose()
        {
            DisposeCount++;
        }
    }
}