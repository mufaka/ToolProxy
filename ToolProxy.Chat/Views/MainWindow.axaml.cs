using Avalonia.Controls;
using Avalonia.Input;
using ToolProxy.Chat.ViewModels;
using System.Threading.Tasks;

namespace ToolProxy.Chat.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void MessageInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            if (DataContext is MainWindowViewModel vm)
            {
                // Fix ReactiveCommand execution - don't await the Observable
                vm.SendMessageCommand.Execute().Subscribe();
            }
        }
    }
}