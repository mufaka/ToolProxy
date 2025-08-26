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
    private bool _isDrawerOpen = false;
    private bool _isLoadingTools = false;


    public MainWindowViewModel()
    {
        _agentService = null;

        // Simple commands for design-time
        SendMessageCommand = ReactiveCommand.Create(() => { });
        ClearHistoryCommand = ReactiveCommand.Create(() => { });
        ToggleDrawerCommand = ReactiveCommand.Create(() => { });
        RefreshToolsCommand = ReactiveCommand.Create(() => { });

        StatusMessage = "Design-time mode";

        // Add some sample tools for design-time
        AvailableServers.Add(new ServerInfo("Clear Thought", "Advanced reasoning tools", new List<ToolInfo>
        {
            new("sequentialthinking", "Dynamic multi-step problem-solving", new()),
            new("mentalmodel", "Structured mental models for problem-solving", new()),
            new("debuggingapproach", "Systematic debugging approaches", new())
        }));
        AvailableServers.Add(new ServerInfo("context7", "Documentation and reference", new List<ToolInfo>
        {
            new("resolve-library-id", "Find library IDs", new()),
            new("get-library-docs", "Get documentation", new())
        }));
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
        RefreshToolsCommand = ReactiveCommand.CreateFromTask(RefreshToolsAsync, outputScheduler: RxApp.MainThreadScheduler);

        // Start initialization as a proper async task
        _ = InitializeAsync();
    }

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<ServerInfo> AvailableServers { get; } = new();

    private int _availableToolsCount;
    public int AvailableToolsCount
    {
        get => _availableToolsCount;
        set => this.RaiseAndSetIfChanged(ref _availableToolsCount, value);
    }

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

    public bool IsLoadingTools
    {
        get => _isLoadingTools;
        set
        {
            this.RaiseAndSetIfChanged(ref _isLoadingTools, value);
        }
    }

    public ReactiveCommand<Unit, Unit> SendMessageCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> ClearHistoryCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> ToggleDrawerCommand { get; private set; }
    public ReactiveCommand<Unit, Unit> RefreshToolsCommand { get; private set; }

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

    public static readonly IValueConverter TotalToolsConverter =
        new FuncValueConverter<ObservableCollection<ServerInfo>, int>(servers =>
            servers?.Sum(s => s.Tools.Count) ?? 0);

    public static readonly IValueConverter HasParametersConverter =
        new FuncValueConverter<Dictionary<string, object>, bool>(parameters =>
            parameters != null && parameters.Count > 0);

    public static readonly IValueConverter ZeroCountToBoolConverter =
        new FuncValueConverter<int, bool>(count => count == 0);

    private void ToggleDrawer()
    {
        IsDrawerOpen = !IsDrawerOpen;
    }

    private async Task RefreshToolsAsync()
    {
        if (_agentService == null) return;

        try
        {
            IsLoadingTools = true;
            StatusMessage = "Refreshing tools...";

            await _agentService.RefreshToolsAsync();
            await LoadToolsAsync();

            StatusMessage = "Tools refreshed successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to refresh tools: {ex.Message}";
        }
        finally
        {
            IsLoadingTools = false;
        }
    }

    private async Task LoadToolsAsync()
    {
        if (_agentService == null) return;

        try
        {
            var servers = await _agentService.GetAvailableToolsAsync();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AvailableServers.Clear();

                int toolCount = 0;
                foreach (var server in servers)
                {
                    AvailableServers.Add(server);
                    toolCount += server.Tools.Count;
                }

                AvailableToolsCount = toolCount;
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load tools: {ex.Message}";
        }
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

            // Load available tools
            await LoadToolsAsync();

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