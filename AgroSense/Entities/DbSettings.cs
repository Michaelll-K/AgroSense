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

        [JsonIgnore]
        public int TasksPerPlayer 
        { 
            get => ShortTasksPerPlayer + LongTasksPerPlayer; 
        }

        public int ShortTasksPerPlayer { get; set; } = 1;
        public int LongTasksPerPlayer { get; set; } = 1;
        public bool IsGameActive { get; set; }
        public int ImpostorsAmount { get; set; } = 1;
        public int ImpostorBlackmailerChance { get; set; } = 0;
        public int ImpostorSniperChance { get; set; } = 0;
        public int DetectiveChance { get; set; } = 0;
        public int DoctorChance { get; set; } = 0;
        public int MayorChance { get; set; } = 0;
        public int SheriffChance { get; set; } = 0;
        public int JesterChance { get; set; } = 0;
        public int RenegateChance { get; set; } = 0;
        public bool AnonymousVoting { get; set; }
        public bool IsPanic { get; set; } = false;
        public string? PanicReporter { get; set; }
        public bool IsCorpse { get; set; } = false;
        public string? CorpseReporter { get; set; }
        public bool IsVoting { get; set; } = false;
        public DateTime? StartDateUtc { get; set; }
        public string? ImpostorsNames { get; set; }
        public string? RenegateHelp { get; set; }
        public DateTime? PanicCooldown { get; set; }
        public int PanicCooldownFromMinutes { get; set; }
        public DateTime? SabotageStartDateUtc { get; set; }
        public DateTime? SabotageCooldown { get; set; }
        public DateTime? SabotageDeadline { get; set; }
        public int SabotageDeadlineFromMinutes { get; set; }
        public int SabotageCooldownFromMinutes { get; set; }
        public int MeetingDurationFromMinutes { get; set; }
        public string? WinnigTeam { get; set; }
        public int CompletedTasksCount { get; set; }
        public bool IsBlackmailUsed { get; set; }
        public bool FirstO2 { get; set; }
        public bool SecondO2 { get; set; }
        public bool IsDetectiveUsed { get; set; }
        public bool IsSniperUsed { get; set; } = false;
        public bool IsSheriffUsed { get; set; } = false;
        public bool IsMayorUsed { get; set; } = false;
        public bool MayorVoted { get; set; } = false;
    }
}
