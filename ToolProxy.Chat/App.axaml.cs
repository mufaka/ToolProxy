using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToolProxy.Chat.Models;
using ToolProxy.Chat.Services;
using ToolProxy.Chat.ViewModels;
using ToolProxy.Chat.Views;

namespace ToolProxy.Chat;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void RegisterServices()
    {
        base.RegisterServices();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var host = CreateHostBuilder().Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = host.Services.GetRequiredService<MainWindow>();
            var mainWindowViewModel = host.Services.GetRequiredService<MainWindowViewModel>();

            // Set the DataContext to connect the View and ViewModel
            mainWindow.DataContext = mainWindowViewModel;

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IHostBuilder CreateHostBuilder() =>
        Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                var chatConfig = context.Configuration.GetSection("Chat").Get<ChatConfiguration>()!;
                services.AddSingleton(chatConfig);

                // Services
                services.AddSingleton<IKernelAgentService, KernelAgentService>();

                // ViewModels and Views
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            });
}