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
        [VectorStoreData(IsIndexed = true)]
        public string ServerName { get; set; } = string.Empty;

        /// <summary>
        /// Name of the tool.
        /// </summary>
        [JsonPropertyName("tool_name")]
        [VectorStoreData(IsIndexed = true)]
        public string ToolName { get; set; } = string.Empty;

        /// <summary>
        /// Description of what the tool does.
        /// </summary>
        [JsonPropertyName("description")]
        [VectorStoreData(IsFullTextIndexed = true)]
        public string Description { get; set; } = string.Empty;

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
        /// 
        /// NOTE: In Memory, currently, only use the Flat index kind regardless of what is specified here. This may change in the future.
        /// NOTE: A Flat index is akin to a table scan in a database. It checks every vector in the collection to find the closest matches. This should
        ///       be fine for this use case as the number of tools is expected to be relatively small.
        /// https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/inmemory-connector?pivots=programming-language-csharp
        /// </summary>
        [JsonPropertyName("embedding")]
        [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity, IndexKind = IndexKind.Flat)]
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
    }
}