using AgroSense.Hubs;
using AgroSense.Utils;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;

namespace AgroSense.Middlewares
{
    public class SendUpdateMiddleware
    {
        private readonly RequestDelegate next;
        private readonly IHubContext<CheckGameHub, ICheckGameClient> hub;
        private readonly IMongoDatabase database;

        public SendUpdateMiddleware(RequestDelegate next, IHubContext<CheckGameHub, ICheckGameClient> hub, IMongoDatabase database)
        {
            this.next = next;
            this.hub = hub;
            this.database = database;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await next(context);

            await hub.SendStatusUpdate(database);
        }
    }
}
