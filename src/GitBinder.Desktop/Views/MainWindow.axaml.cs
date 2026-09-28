using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using GitBinder.Desktop.ViewModels;

namespace GitBinder.Desktop.Views;

public partial class MainWindow : Window
{
    private bool _isInitializing;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.Handled || OwnedWindows.Any(window => window.IsVisible)) return;

        // 下拉框是最内层操作；同一次 Escape 不能继续关闭编辑区或主窗口。
        var openComboBox = this.GetVisualDescendants().OfType<ComboBox>()
            .FirstOrDefault(comboBox => comboBox.IsDropDownOpen);
        if (openComboBox is null) return;
        openComboBox.IsDropDownOpen = false;
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || e.Handled) return;

        // 模态窗口自行处理 Escape，不把子窗口的关闭动作传递给主窗口。
        if (e.Source is Visual source && !ReferenceEquals(TopLevel.GetTopLevel(source), this)) return;
        e.Handled = true;
        if (OwnedWindows.Any(window => window.IsVisible)) return;

        var projectsView = this.GetVisualDescendants().OfType<ProjectsView>()
            .FirstOrDefault(view => view.IsVisible);
        if (projectsView?.TryCancelInlineEdit(FocusManager?.GetFocusedElement() as Control) is true) return;
        if (_isInitializing || DataContext is MainViewModel { CanCloseWithEscape: false }) return;

        // 复用右上角关闭入口：根据现有设置隐藏到托盘或退出，不直接 Shutdown。
        Close();
    }

    private async void OnOpened(object? sender, System.EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            _isInitializing = true;
            try
            {
                await vm.InitializeAsync();
            }
            finally
            {
                _isInitializing = false;
            }
        }
    }
}
