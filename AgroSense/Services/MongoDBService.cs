using MongoDB.Driver;

namespace AgroSense.Services
{
    public static class MongoDBService
    {
        public static IMongoDatabase CreateAsync(IConfiguration configuration)
        {
            //var client = new MongoClient(configuration["MongoDB:Endpoint"]);
            //return client.GetDatabase(configuration["MongoDB:DbName"]);

            return null;
        }
    }
}
