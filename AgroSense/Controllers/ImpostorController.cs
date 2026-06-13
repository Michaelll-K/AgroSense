using Azure;
using Azure.Data.Tables;
using AgroSense.Entities;
using AgroSense.Enums;
using AgroSense.Utils;
using Microsoft.AspNetCore.Mvc;

namespace AgroSense.Controllers
{
    [ApiController]
    [Route("api/impostor")]
    public class ImpostorController : ControllerBase
    {
        private readonly TableServiceClient tableService;

        #region ImpostorController()
        public ImpostorController(TableServiceClient tableService)
        {
            this.tableService = tableService;
        }
        #endregion

        #region Blackmail()
        [HttpPost("{name}/blackmail/{blackmailName}")]
        public async Task<ActionResult> Blackmail(string name, string blackmailName)
        {
            var settings = await tableService.GetSettings();
            var currentPlayer = await tableService.GetPlayer(name);
            var blackmailPlayer = await tableService.GetPlayer(blackmailName);

            if (currentPlayer is null || blackmailPlayer is null || !currentPlayer.Role.Contains(Role.Impostor.ToString()))
                return NotFound();

            if (settings.IsBlackmailUsed)
                return BadRequest("Blackmail został już wykorzystany!");

            blackmailPlayer.IsBlackmailed = true;

            var playersClient = tableService.GetTableClient(DbPlayer.TableName);
            await playersClient.UpdateEntityAsync(blackmailPlayer, ETag.All, TableUpdateMode.Replace);

            settings.IsBlackmailUsed = true;

            var settingsClient = tableService.GetTableClient(DbSettings.TableName);
            await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            return Accepted();
        }
        #endregion

        #region Sabotage()
        [HttpPost("{name}/sabotage/{delay}")]
        public async Task<ActionResult> Sabotage(string name, int delay)
        {
            var settings = await tableService.GetSettings();
            var currentPlayer = await tableService.GetPlayer(name);

            if (currentPlayer is null || !currentPlayer.Role.Contains(Role.Impostor.ToString()))
                return NotFound();

            if (settings.SabotageCooldown > DateTime.UtcNow)
                return BadRequest("Sabotaż jeszcze się nie odnowił");

            settings.SabotageStartDateUtc = DateTime.UtcNow.AddMinutes(delay);
            settings.SabotageDeadline = settings.SabotageStartDateUtc.Value.AddMinutes(settings.SabotageDeadlineFromMinutes);
            settings.SabotageCooldown = settings.SabotageDeadline;

            var settingsClient = tableService.GetTableClient(DbSettings.TableName);
            await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            return Accepted();
        }
        #endregion

        #region GetUsersToBlackmail()
        [HttpGet("users-to-blackmail")]
        public async Task<ActionResult<List<DbPlayer>>> GetUsersToBlackmail()
        {
            var tableClient = tableService.GetTableClient(DbPlayer.TableName);
            var players = new List<DbPlayer>();
            await foreach (var player in tableClient.QueryAsync<DbPlayer>())
                players.Add(player);
            return players.Where(p => p.IsAlive).ToList();
        }
        #endregion
    }
}
