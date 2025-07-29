using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace AgroSense.Entities
{
    public class DbTask
    {
        public static string DbName => nameof(DbTask).ToLower();

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
    }
}
