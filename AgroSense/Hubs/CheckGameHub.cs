using AgroSense.Models.Player;
using Microsoft.AspNetCore.SignalR;

namespace AgroSense.Hubs
{
    public interface ICheckGameClient
    {
        Task AmogusStatus(CheckGameModel checkGameModel);
    }

    public class CheckGameHub : Hub<ICheckGameClient>
    {
    }
}
