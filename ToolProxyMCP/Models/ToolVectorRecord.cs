using Microsoft.Extensions.VectorData;
using System.Text.Json.Serialization;

namespace ToolProxy.Models
{
    /// <summary>
    /// Vector store model for indexing MCP tool information with semantic search capabilities.
    /// </summary>
    public sealed class ToolVectorRecord
    {
        /// <summary>
        /// Unique identifier for the tool record. Format: {ServerName}.{ToolName}
        /// </summary>
        [JsonPropertyName("id")]
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Name of the MCP server that provides this tool.
        /// </summary>
        [JsonPropertyName("server_name")]
        [VectorStoreData]
        public string ServerName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the tool.
        /// </summary>
        [JsonPropertyName("tool_name")]
        [VectorStoreData]
        public string ToolName { get; set; } = string.Empty;

        /// <summary>
        /// Description of what the tool does.
        /// </summary>
        [JsonPropertyName("description")]
        [VectorStoreData]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Concatenated text used for generating embeddings (tool name + description).
        /// </summary>
        [JsonPropertyName("searchable_text")]
        [VectorStoreData]
        public string SearchableText { get; set; } = string.Empty;

        /// <summary>
        /// JSON representation of tool parameters for detailed information.
        /// </summary>
        [JsonPropertyName("parameters_json")]
        [VectorStoreData]
        public string ParametersJson { get; set; } = string.Empty;

        /// <summary>
        /// Number of parameters the tool accepts.
        /// </summary>
        [JsonPropertyName("parameter_count")]
        [VectorStoreData]
        public int ParameterCount { get; set; }

        /// <summary>
        /// Comma-separated list of parameter names.
        /// </summary>
        [JsonPropertyName("parameter_names")]
        [VectorStoreData]
        public string ParameterNames { get; set; } = string.Empty;

        /// <summary>
        /// Vector embedding of the searchable text for semantic similarity search.
        /// </summary>
        [JsonPropertyName("embedding")]
        [VectorStoreVector(Dimensions: 512, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Hnsw)]
        public ReadOnlyMemory<float> Embedding { get; set; }

        /// <summary>
        /// Timestamp when this record was last updated.
        /// </summary>
        [JsonPropertyName("last_updated")]
        [VectorStoreData]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// Creates a unique ID for the tool record.
        /// </summary>
        public static string CreateId(string serverName, string toolName)
        {
            return $"{serverName}.{toolName}";
        }

        /// <summary>
        /// Creates searchable text from tool name and description.
        /// </summary>
        public static string CreateSearchableText(string toolName, string description)
        {
            return $"{toolName}: {description}".Trim();
        }
    }
}