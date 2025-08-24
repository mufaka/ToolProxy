using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ToolProxy.Services
{
    /// <summary>
    /// Text embedding generation service that uses Ollama for generating embeddings.
    /// </summary>
    public class OllamaTextEmbeddingGenerationService : ITextEmbeddingGenerationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _modelName;
        private readonly ILogger<OllamaTextEmbeddingGenerationService> _logger;

        public OllamaTextEmbeddingGenerationService(
            HttpClient httpClient,
            string modelName,
            ILogger<OllamaTextEmbeddingGenerationService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _modelName = modelName ?? throw new ArgumentNullException(nameof(modelName));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>
        {
            { "model", _modelName },
            { "provider", "Ollama" }
        };

        public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
            IList<string> data,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<ReadOnlyMemory<float>>();

            foreach (var text in data)
            {
                var embedding = await GenerateEmbeddingAsync(text, kernel, cancellationToken);
                results.Add(embedding);
            }

            return results;
        }

        public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
            string text,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new OllamaEmbeddingRequest
                {
                    Model = _modelName,
                    Input = text
                };

                _logger.LogDebug("Generating embedding for text with Ollama model {Model}", _modelName);

                var response = await _httpClient.PostAsJsonAsync(
                    "/api/embed",
                    request,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                var embeddingResponse = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(
                    cancellationToken: cancellationToken);

                if (embeddingResponse?.Embeddings == null || !embeddingResponse.Embeddings.Any())
                {
                    throw new InvalidOperationException("No embeddings returned from Ollama");
                }

                // Ollama returns embeddings as double arrays, convert to float
                var embedding = embeddingResponse.Embeddings.First();
                var floatEmbedding = embedding.Select(d => (float)d).ToArray();

                _logger.LogDebug("Generated embedding with {Dimensions} dimensions", floatEmbedding.Length);

                return new ReadOnlyMemory<float>(floatEmbedding);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for text with Ollama");
                throw;
            }
        }
    }

    /// <summary>
    /// Request model for Ollama embedding API.
    /// </summary>
    internal class OllamaEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("input")]
        public string Input { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for Ollama embedding API.
    /// </summary>
    internal class OllamaEmbeddingResponse
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("embeddings")]
        public double[][] Embeddings { get; set; } = [];
    }
}