using MongoDB.Driver;
using System.Security.Authentication;

namespace AgroSense.Services
{
    public static class MongoDBService
    {
        public static IMongoDatabase CreateAsync(IConfiguration configuration)
        {
            var settings = MongoClientSettings.FromUrl(new MongoUrl(configuration["MongoDB:Endpoint"]));

            settings.SslSettings = new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
            
            settings.RetryWrites = false;
            
            var client = new MongoClient(settings);
            return client.GetDatabase(configuration["MongoDB:DbName"]);
        }
    }
}
