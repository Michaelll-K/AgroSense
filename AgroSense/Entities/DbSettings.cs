using Azure;
using Azure.Data.Tables;
using System.Text.Json.Serialization;

namespace AgroSense.Entities
{
    public class DbSettings : ITableEntity
    {
        public static string TableName => "Settings";

        public string PartitionKey { get; set; } = "Settings";
        public string RowKey { get; set; } = "main";
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        [JsonIgnore]
        public string Id
        {
            get => RowKey;
            set => RowKey = value;
        }

        public bool IsGameActive { get; set; }
        public int TaskPerPlayer { get; set; } = 1;
        public int ImpostorsAmount { get; set; } = 1;
        public int DetectivesAmount { get; set; } = 0;
        public int DoctorsAmount { get; set; } = 0;
        public bool IsPanic { get; set; } = false;
        public string? PanicReporter { get; set; }
        public bool IsCorpse { get; set; } = false;
        public string? CorpseReporter { get; set; }
        public DateTime? StartDateUtc { get; set; }
        public string? ImpostorsNames { get; set; }
        public DateTime? PanicCooldown { get; set; }
        public int PanicCooldownFromMinutes { get; set; }
        public DateTime? SabotageStartDateUtc { get; set; }
        public DateTime? SabotageCooldown { get; set; }
        public DateTime? SabotageDeadline { get; set; }
        public int SabotageDeadlineFromMinutes { get; set; }
        public int SabotageCooldownFromMinutes { get; set; }
        public string? WinnigTeam { get; set; }
        public int CompletedTasksCount { get; set; }
        public bool IsBlackmailUsed { get; set; }
        public bool FirstO2 { get; set; }
        public bool SecondO2 { get; set; }
        public bool IsDetectiveUsed { get; set; }
    }
}
