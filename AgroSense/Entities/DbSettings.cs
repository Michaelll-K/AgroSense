using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace AgroSense.Entities
{
    public class DbSettings
    {
        public static string DbName => nameof(DbSettings).ToLower();

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public bool IsGameActive { get; set; }
        public int TaskPerPlayer { get; set; } = 1;
        public int ImpostorsAmount { get; set; } = 1;
        public int DetectivesAmount { get; set; } = 0;
        public int DoctorsAmount { get; set; } = 0;
        public bool IsPanic { get; set; } = false;
        public string PanicReporter { get; set; }
        public bool IsCorpse { get; set; } = false;
        public string CorpseReporter { get; set; }
        public DateTime? StartDateUtc { get; set; }
        public string ImpostorsNames { get; set; }
        public DateTime? PanicCooldown { get; set; }
        public int PanicCooldownFromMinutes { get; set; }
        public DateTime? SabotageStartDateUtc { get; set; }
        public DateTime? SabotageCooldown { get; set; }
        public DateTime? SabotageDeadline { get; set; }
        public int SabotageDeadlineFromMinutes { get; set; }
        public int SabotageCooldownFromMinutes { get; set; }
        public string WinnigTeam { get; set; }
        public int CompletedTasksCount { get; set; }
        public bool IsBlackmailUsed { get; set; }
        public bool FirstO2 { get; set; }
        public bool SecondO2 { get; set; }
        public bool IsDetectiveUsed { get; set; }
    }
}
