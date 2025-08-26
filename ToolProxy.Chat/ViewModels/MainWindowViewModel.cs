using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using ToolProxy.Chat.Models;
using ToolProxy.Chat.Services;

namespace ToolProxy.Chat.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IKernelAgentService? _agentService;
    private string _currentMessage = string.Empty;
    private string _statusMessage = "Initializing...";
    private bool _isProcessing;
    private bool _isConnected;
    private bool _isDrawerOpen = true;


    public MainWindowViewModel()
    {
        _agentService = null;

        // Simple commands for design-time
        SendMessageCommand = ReactiveCommand.Create(() => { });
        ClearHistoryCommand = ReactiveCommand.Create(() => { });
        ToggleDrawerCommand = ReactiveCommand.Create(() => { });

        StatusMessage = "Design-time mode";
    }

    // Runtime constructor for dependency injection
    public MainWindowViewModel(IKernelAgentService agentService)
    {
        _agentService = agentService;

        // Use MainThreadScheduler to ensure commands run on UI thread
        SendMessageCommand = ReactiveCommand.CreateFromTask(SendMessageAsync, outputScheduler: RxApp.MainThreadScheduler);
        //SendMessageCommand = ReactiveCommand.Create(() => { /* do nothing */ });
        ClearHistoryCommand = ReactiveCommand.CreateFromTask(ClearHistoryAsync, outputScheduler: RxApp.MainThreadScheduler);
        ToggleDrawerCommand = ReactiveCommand.Create(ToggleDrawer, outputScheduler: RxApp.MainThreadScheduler);

        // Start initialization as a proper async task
        _ = InitializeAsync();
    }

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public string CurrentMessage
    {
        get => _currentMessage;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentMessage, value);
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            this.RaiseAndSetIfChanged(ref _isProcessing, value);
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            this.RaiseAndSetIfChanged(ref _isConnected, value);
        }
    }

    public bool IsDrawerOpen
    {
        get => _isDrawerOpen;
        set
        {
            this.RaiseAndSetIfChanged(ref _isDrawerOpen, value);
        }
    }

    public ReactiveCommand<Unit, Unit> SendMessageCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> ClearHistoryCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> ToggleDrawerCommand { get; private set; }

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

    private void ToggleDrawer()
    {
        IsDrawerOpen = !IsDrawerOpen;
    }

    private async Task InitializeAsync()
    {
        if (_agentService == null)
            return; // Skip initialization in design mode

        try
        {
            // Update UI on the UI thread
            await Dispatcher.UIThread.InvokeAsync(() => StatusMessage = "Connecting to ToolProxy...");

            // Perform the actual initialization (can be on background thread)
            await _agentService.InitializeAsync();

            // Update UI on the UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsConnected = true;
                StatusMessage = "Ready - Type your message and press Enter";
            });
        }
        catch (Exception ex)
        {
            // Update UI on the UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusMessage = $"Connection failed: {ex.Message}";
                IsConnected = false;
            });
        }
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentMessage) || IsProcessing || _agentService == null)
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

            var response = await _agentService.InvokeAsync(userMessage);

            // Add assistant response to UI
            var assistantMessage = new ChatMessage
            {
                Content = response,
                Role = ChatRole.Assistant
            };

            Messages.Add(assistantMessage);

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

    private Task ClearHistoryAsync()
    {
        Messages.Clear();
        StatusMessage = "History cleared - Ready for new conversation";
        if (_agentService != null)
        {
            _agentService.ClearHistoryAsync();
        }
        return Task.CompletedTask;
    }
}

public class ViewModelBase : ReactiveObject
{
}