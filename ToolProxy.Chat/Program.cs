using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.Hosting;

namespace ToolProxy.Chat;

public class Program
{
    public static IHost? AppHost { get; private set; }

    //[STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}