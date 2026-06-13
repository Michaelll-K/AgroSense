using AgroSense.Enums;
using Azure;
using Azure.Data.Tables;
using System.Text.Json.Serialization;

namespace AgroSense.Entities
{
    public class DbTask : ITableEntity
    {
        public static string TableName => "Task";

        public string PartitionKey { get; set; } = "Task";
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        [JsonIgnore]
        public string Id
        {
            get => RowKey;
            set => RowKey = value;
        }

        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? Description { get; set; }
        public TaskLength TaskLength { get; set; }
    }
}
