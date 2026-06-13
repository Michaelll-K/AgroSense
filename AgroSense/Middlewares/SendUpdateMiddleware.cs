using Azure.Data.Tables;
using AgroSense.Hubs;
using AgroSense.Utils;
using Microsoft.AspNetCore.SignalR;

namespace AgroSense.Middlewares
{
    public class SendUpdateMiddleware
    {
        private readonly RequestDelegate next;
        private readonly IHubContext<CheckGameHub, ICheckGameClient> hub;
        private readonly TableServiceClient tableService;

        public SendUpdateMiddleware(RequestDelegate next, IHubContext<CheckGameHub, ICheckGameClient> hub, TableServiceClient tableService)
        {
            this.next = next;
            this.hub = hub;
            this.tableService = tableService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await next(context);

            if (context.Request.Path.StartsWithSegments("/api/admin") ||
                context.Request.Path.StartsWithSegments("/api/hq") ||
                context.Request.Path.StartsWithSegments("/api/impostor") ||
                context.Request.Path.StartsWithSegments("/api/player"))
            {
                await hub.SendStatusUpdate(tableService);
            }
        }
    }
}
