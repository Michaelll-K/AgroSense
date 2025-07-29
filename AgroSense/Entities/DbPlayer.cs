using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace AgroSense.Entities
{
    public class DbPlayer
    {
        public static string DbName => nameof(DbPlayer).ToLower();

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public string TasksJson { get; set; }
        public bool IsAlive { get; set; }
        public bool IsBlackmailed { get; set; }
    }
}
