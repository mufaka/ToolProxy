using Avalonia.Controls;
using Avalonia.Input;

namespace ToolProxy.Chat.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        MessageInput.AddHandler(KeyDownEvent, OnMessageInputPreviewKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnMessageInputPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (DataContext is ViewModels.MainWindowViewModel vm)
            {
                vm.SendMessageCommand.Execute();
            }
            e.Handled = true;
        }
    }
}