using Azure.Data.Tables;

namespace AgroSense.Services
{
    public static class AzureTableService
    {
        public static TableServiceClient Create(IConfiguration configuration)
        {
            return new TableServiceClient(configuration["AzureStorage:ConnectionString"]);
        }
    }
}
