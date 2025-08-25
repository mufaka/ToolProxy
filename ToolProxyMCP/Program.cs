using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Embeddings;
using ToolProxy.Configuration;
using ToolProxy.Services;

namespace ToolProxy
{
    public record SearchToolsRequest(string Prompt, int MaxResults = 5, float MinRelevanceScore = 0.3f);

    internal class Program
    {
        static async Task Main(string[] args)
        {
            var debugMode = args.Contains("--debug", StringComparer.OrdinalIgnoreCase);

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
            services.AddSingleton(appSettings.SemanticKernel);
            services.AddSingleton<IMcpManager, McpManager>();

            ConfigureSemanticKernelServices(services, appSettings.SemanticKernel);

            services.AddHostedService<McpHostedService>();

            services.AddMcpServer()
                .WithHttpTransport()
                .WithToolsFromAssembly();

            var app = builder.Build();

            app.UseRouting();
            app.MapMcp("/mcp");
            app.MapGet("/health", () => "MCP Server is running");

            app.MapGet("/tool-index-info", (IToolIndexService toolIndexService) => new
            {
                ServiceType = toolIndexService.GetType().Name,
                IsSemanticKernelEnabled = true,
                IsIndexReady = toolIndexService.IsIndexReady
            });

            app.MapPost("/search-tools", async (SearchToolsRequest request, IToolIndexService toolIndexService) =>
            {
                if (!toolIndexService.IsIndexReady)
                {
                    return Results.BadRequest("Tool index is not ready. Please wait for indexing to complete.");
                }

                if (string.IsNullOrWhiteSpace(request.Prompt))
                {
                    return Results.BadRequest("Prompt cannot be empty.");
                }

                try
                {
                    var searchResults = await toolIndexService.SearchToolsSemanticAsync(
                        request.Prompt,
                        request.MaxResults,
                        request.MinRelevanceScore);

                    var response = new
                    {
                        Query = request.Prompt,
                        MaxResults = request.MaxResults,
                        MinRelevanceScore = request.MinRelevanceScore,
                        Tools = searchResults.Select(result => new
                        {
                            ServerName = result.ServerName,
                            Name = result.Tool.Name,
                            Description = result.Tool.Description,
                            RelevanceScore = result.RelevanceScore,
                            Parameters = result.Tool.Parameters.Select(p => new
                            {
                                Name = p.Name,
                                Type = p.Type,
                                Description = p.Description,
                                Required = p.IsRequired
                            })
                        }).ToList()
                    };

                    return Results.Ok(response);
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Error searching tools: {ex.Message}");
                }
            });

            // Configure the server to listen on the specified port
            var mcpPort = configuration.GetValue<int>("McpServer:Port", 3030);
            var mcpHost = configuration.GetValue<string>("McpServer:Host", "localhost");

            app.Urls.Add($"http://{mcpHost}:{mcpPort}");

            Console.WriteLine($"Starting MCP Server with Semantic Kernel tool indexing...");

            await app.RunAsync();
        }

        private static void ConfigureSemanticKernelServices(IServiceCollection services, SemanticKernelSettings settings)
        {
            // Add HTTP client for Ollama
            services.AddHttpClient("Ollama", client =>
            {
                client.BaseAddress = new Uri(settings.Ollama.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(5); // Embedding generation can take time
            });

            // Register Ollama embedding service
            services.AddSingleton<ITextEmbeddingGenerationService>(serviceProvider =>
            {
                var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient("Ollama");
                var logger = serviceProvider.GetRequiredService<ILogger<OllamaTextEmbeddingGenerationService>>();

                return new OllamaTextEmbeddingGenerationService(httpClient, settings.Ollama.ModelName, logger);
            });

            // Register the Semantic Kernel-based tool index service
            services.AddSingleton<IToolIndexService, SemanticKernelToolIndexService>();

            Console.WriteLine($"Semantic Kernel configured with Ollama embeddings: {settings.Ollama.BaseUrl} / {settings.Ollama.ModelName}");
        }
    }
}
