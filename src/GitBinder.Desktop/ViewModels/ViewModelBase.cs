using CommunityToolkit.Mvvm.ComponentModel;

namespace GitBinder.Desktop.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>操作结果统一弹窗；相同结果重复发生时仍然通知，空值只清理状态。</summary>
    protected void SetNotice(ref string field, string value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        SetProperty(ref field, value, propertyName);
        if (!string.IsNullOrWhiteSpace(value)) DialogHelper.Notify(value, this);
    }
}
