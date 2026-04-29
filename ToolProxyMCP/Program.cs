using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ToolProxy.Configuration;
using ToolProxy.Services;

namespace ToolProxy
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            var builder = WebApplication.CreateBuilder(args);
            var services = builder.Services;

            services.AddLogging(builder =>
            {
                builder.AddConsole().AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                });

                builder.AddConfiguration(configuration.GetSection("Logging"));
            });

            var appSettings = new AppSettings();
            configuration.Bind(appSettings);

            services.AddSingleton(appSettings);
            services.AddSingleton<IMcpManager, McpManager>();
            services.AddSingleton<IMcpDispatcher, McpDispatcher>();

            services.AddHostedService<McpHostedService>();

            services.AddMcpServer()
                .WithHttpTransport()
                .WithToolsFromAssembly();

            services.AddOptions<McpServerOptions>().Configure(options =>
            {
                options.ServerInstructions = "ToolProxy fronts a curated fleet of upstream MCP servers. Use the toolproxy-* skills installed in this project to discover and call upstream tools via call_external_tool.";
            });

            var app = builder.Build();

            app.UseRouting();
            app.MapMcp("/mcp");
            app.MapGet("/health", () => "MCP Server is running");

            var mcpPort = configuration.GetValue<int>("McpServer:Port", 3030);
            var mcpHost = configuration.GetValue<string>("McpServer:Host", "localhost");

            app.Urls.Add($"http://{mcpHost}:{mcpPort}");

            Console.WriteLine($"Starting ToolProxy MCP server on http://{mcpHost}:{mcpPort}");

            await app.RunAsync();
        }
    }
}
