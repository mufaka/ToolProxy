using Avalonia.Controls;
using Avalonia.Input;
using System.Collections.Specialized;
using ToolProxy.Chat.ViewModels;

namespace ToolProxy.Chat.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        MessageInput.AddHandler(KeyDownEvent, OnMessageInputPreviewKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // Subscribe to DataContext changes to hook up auto-scroll
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            // Subscribe to collection changes to auto-scroll
            viewModel.Messages.CollectionChanged += OnMessagesCollectionChanged;
        }
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            // Scroll to bottom when new messages are added
            ChatScrollViewer.ScrollToEnd();
        }
    }

    private void OnMessageInputPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.SendMessageCommand.Execute();
            }
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Clean up event subscriptions
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Messages.CollectionChanged -= OnMessagesCollectionChanged;
        }
        base.OnClosed(e);
    }
}