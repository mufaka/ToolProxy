using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;
using OllamaSharp;
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

            services.AddOptions<McpServerOptions>().Configure(options =>
            {
                options.ServerInstructions = @"When you are asked to perform actions, always check with me to see if I can help. Use my semantic search to find tools that are relevant to what you are trying to do. If I do have tools that I think match your request, I will return those tools along with an example of how to execute them. It is very important that you use those examples for how you format your tool request. Do not change the provided serverName or toolName. You should only update the parameters for the tool to be relevant to the task you are asked to perform.";
            });

            var app = builder.Build();

            app.UseRouting();
            app.MapMcp("/mcp");
            app.MapGet("/health", () => "MCP Server is running");

            app.MapGet("/tool-index-info", (IToolIndexService toolIndexService) => new
            {
                ServiceType = toolIndexService.GetType().Name,
                IsSemanticKernelEnabled = true,
            });

            app.MapPost("/search-tools", async (SearchToolsRequest request, IToolIndexService toolIndexService) =>
            {
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
            // Add HTTP client for Ollama embeddings
            services.AddHttpClient("OllamaEmbedding", client =>
            {
                client.BaseAddress = new Uri(settings.OllamaEmbedding.BaseUrl);
                client.Timeout = TimeSpan.FromMinutes(5); // Embedding generation can take time
            });

            // inject the OllamaSharp client as IEmbeddingGenerator. It implements several interfaces including the IEmbeddingGenerator that
            // we need to get embeddings for semantic search. ITextEmbeddingGenerationService is deprecated.
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(serviceProvider =>
            {
                var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient("OllamaEmbedding");
                var ollamaClient = new OllamaApiClient(httpClient, settings.OllamaEmbedding.ModelName);

                IEmbeddingGenerator<string, Embedding<float>> generator = ollamaClient;
                return generator;
            });

            if (settings.UseEnhancedPhraseGeneration)
            {
                // Add HTTP client for Ollama chat completions (may be the same endpoint as embeddings)
                services.AddHttpClient("OllamaChat", client =>
                {
                    client.BaseAddress = new Uri(settings.OllamaChat.BaseUrl);
                    client.Timeout = TimeSpan.FromMinutes(5); // Chat completion can take time
                });

                // Add Semantic Kernel with Ollama chat completion service for enhanced phrase generation
                services.AddKernel()
                    .AddOllamaChatCompletion(
                        modelId: settings.OllamaChat.ModelName,
                        endpoint: new Uri(settings.OllamaChat.BaseUrl));

                // Register the Enhanced Semantic Kernel-based tool index service with chat completion support
                services.AddSingleton<IToolIndexService, EnhancedSemanticKernelToolIndexService>();

                Console.WriteLine($"Enhanced Semantic Kernel configured with Ollama embeddings: {settings.OllamaEmbedding.BaseUrl} / {settings.OllamaEmbedding.ModelName}");
                Console.WriteLine($"Enhanced Semantic Kernel configured with Ollama chat completions: {settings.OllamaChat.BaseUrl} / {settings.OllamaChat.ModelName}");
            }
            else
            {
                // Register the standard Semantic Kernel-based tool index service
                services.AddSingleton<IToolIndexService, SemanticKernelToolIndexService>();

                Console.WriteLine($"Standard Semantic Kernel configured with Ollama embeddings: {settings.OllamaEmbedding.BaseUrl} / {settings.OllamaEmbedding.ModelName}");
            }
        }
    }
}
