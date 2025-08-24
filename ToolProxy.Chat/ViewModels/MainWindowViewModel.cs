using Avalonia.Data.Converters;
using Avalonia.Media;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using ToolProxy.Chat.Models;
using ToolProxy.Chat.Services;

namespace ToolProxy.Chat.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IKernelAgentService _agentService;
    private string _currentMessage = string.Empty;
    private string _statusMessage = "Initializing...";
    private bool _isProcessing;
    private bool _isConnected;

    public MainWindowViewModel(IKernelAgentService agentService)
    {
        _agentService = agentService;

        SendMessageCommand = ReactiveCommand.CreateFromTask(SendMessageAsync);
        ClearHistoryCommand = ReactiveCommand.CreateFromTask(ClearHistoryAsync);

        _ = InitializeAsync();
    }

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public string CurrentMessage
    {
        get => _currentMessage;
        set => this.RaiseAndSetIfChanged(ref _currentMessage, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set => this.RaiseAndSetIfChanged(ref _isProcessing, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    public ReactiveCommand<Unit, Unit> SendMessageCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearHistoryCommand { get; }

    // Static converters for the UI
    public static readonly IValueConverter MessageBackgroundConverter =
        new FuncValueConverter<ChatRole, IBrush>(role => role switch
        {
            ChatRole.User => new SolidColorBrush(Color.Parse("#E3F2FD")),
            ChatRole.Assistant => new SolidColorBrush(Color.Parse("#F1F8E9")),
            ChatRole.System => new SolidColorBrush(Color.Parse("#FFF3E0")),
            _ => new SolidColorBrush(Color.Parse("#F5F5F5"))
        });

    public static readonly IValueConverter MessageAlignmentConverter =
        new FuncValueConverter<ChatRole, Avalonia.Layout.HorizontalAlignment>(role =>
            role == ChatRole.User ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left);

    public static readonly IValueConverter BooleanToBrushConverter =
        new FuncValueConverter<bool, IBrush>(connected =>
            connected ? new SolidColorBrush(Color.Parse("#4CAF50")) : new SolidColorBrush(Color.Parse("#F44336")));

    private async Task InitializeAsync()
    {
        try
        {
            StatusMessage = "Connecting to ToolProxy...";
            await _agentService.InitializeAsync();
            IsConnected = true;
            StatusMessage = "Ready - Type your message and press Enter";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            IsConnected = false;
        }
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentMessage) || IsProcessing)
            return;

        var userMessage = CurrentMessage;
        CurrentMessage = string.Empty;
        IsProcessing = true;
        StatusMessage = "Processing...";

        try
        {
            // Add user message to UI
            Messages.Add(new ChatMessage
            {
                Content = userMessage,
                Role = ChatRole.User
            });

            // Create assistant message for streaming updates
            var assistantMessage = new ChatMessage
            {
                Content = string.Empty,
                Role = ChatRole.Assistant
            };
            Messages.Add(assistantMessage);

            // Stream response from kernel
            var responseBuilder = new System.Text.StringBuilder();
            await foreach (var chunk in _agentService.InvokeStreamingAsync(userMessage))
            {
                responseBuilder.Append(chunk);

                // Update the last message in real-time
                var updatedMessage = assistantMessage with
                {
                    Content = responseBuilder.ToString()
                };
                Messages[Messages.Count - 1] = updatedMessage;
            }

            StatusMessage = "Ready - Type your message and press Enter";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Messages.Add(new ChatMessage
            {
                Content = $"Error: {ex.Message}",
                Role = ChatRole.System
            });
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task ClearHistoryAsync()
    {
        Messages.Clear();
        await _agentService.ClearHistoryAsync();
        StatusMessage = "History cleared - Ready for new conversation";
    }
}

public class ViewModelBase : ReactiveObject
{
}