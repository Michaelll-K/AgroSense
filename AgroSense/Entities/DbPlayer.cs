using Azure;
using Azure.Data.Tables;
using System.Text.Json.Serialization;

namespace AgroSense.Entities
{
    public class DbPlayer : ITableEntity
    {
        public static string TableName => "Player";

        public string PartitionKey { get; set; } = "Player";
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
        public string Role { get; set; } = string.Empty;
        public string? TasksJson { get; set; }
        public bool IsAlive { get; set; }
        public bool IsBlackmailed { get; set; }
        public string? VotedPerson { get; set; }
    }
}
