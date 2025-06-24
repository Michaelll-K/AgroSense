using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AgroSense.Entities
{
    public class HumidityHistory
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public DateTime DateUtc { get; set; }
        public int Humidity { get; set; }
    }
}
