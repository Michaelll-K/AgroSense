using Azure;
using Azure.Data.Tables;
using AgroSense.Entities;
using AgroSense.Enums;
using AgroSense.Utils;
using Microsoft.AspNetCore.Mvc;
using AgroSense.Services;

namespace AgroSense.Controllers
{
    [ApiController]
    [Route("api/hq")]
    public class HeadquartersController : ControllerBase
    {
        private readonly TableServiceClient tableService;
        private readonly AmogusService amogusService;

        #region HeadquartersController()
        public HeadquartersController(TableServiceClient tableService, AmogusService amogusService)
        {
            this.tableService = tableService;
            this.amogusService = amogusService;
        }
        #endregion

        #region StartPanic()
        [HttpPost("{name}/start-panic")]
        public async Task<ActionResult<DateTime>> StartPanic(string name)
        {
            var player = await tableService.GetPlayer(name);

            if (player is null)
                return NotFound();

            var tableClient = tableService.GetTableClient(DbSettings.TableName);
            var settings = (await tableClient.GetEntityAsync<DbSettings>("Settings", "main")).Value;

            if (settings.PanicCooldown > DateTime.UtcNow)
                return BadRequest("Przycisk paniki jeszcze się nie odnowił!");

            settings.IsPanic = true;
            settings.PanicReporter = name;

            await tableClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            return Accepted();
        }
        #endregion

        #region EndPanic()
        [HttpPost("end-panic")]
        public async Task<ActionResult<DateTime>> EndPanic()
        {
            var tableClient = tableService.GetTableClient(DbSettings.TableName);
            var settings = (await tableClient.GetEntityAsync<DbSettings>("Settings", "main")).Value;

            settings.IsPanic = false;
            settings.PanicCooldown = DateTime.UtcNow.AddMinutes(settings.PanicCooldownFromMinutes);
            settings.IsCorpse = false;
            settings.IsBlackmailUsed = false;
            settings.IsVoting = false;

            await tableClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            var playersClient = tableService.GetTableClient(DbPlayer.TableName);
            var players = new List<DbPlayer>();
            await foreach (var player in playersClient.QueryAsync<DbPlayer>())
                players.Add(player);

            foreach (var player in players)
            {
                player.IsBlackmailed = false;
                player.VotedPerson = null;
                await playersClient.UpdateEntityAsync(player, ETag.All, TableUpdateMode.Replace);
            }

            return Accepted();
        }
        #endregion

        #region CheckSabotage()
        [HttpGet("check-sabotage")]
        public async Task<ActionResult> CheckSabotage()
        {
            var settings = await tableService.GetSettings();

            if (settings.SabotageDeadline <= DateTime.UtcNow)
            {
                settings.IsGameActive = false;
                settings.WinnigTeam = Role.Impostor.ToString();

                var tableClient = tableService.GetTableClient(DbSettings.TableName);
                await tableClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);
            }

            return Ok();
        }
        #endregion

        #region FirstO2()
        [HttpPost("firstO2/{isPressed}")]
        public async Task<ActionResult> FirstO2(bool isPressed)
        {
            var settings = await tableService.GetSettings();

            if (!settings.SabotageDeadline.HasValue)
                return Ok();

            settings.FirstO2 = isPressed;

            if (settings.FirstO2 && settings.SecondO2)
            {
                settings.SabotageDeadline = null;
                settings.FirstO2 = false;
                settings.SecondO2 = false;
                settings.SabotageCooldown = DateTime.UtcNow.AddMinutes(settings.SabotageCooldownFromMinutes);
            }

            var tableClient = tableService.GetTableClient(DbSettings.TableName);
            await tableClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            return Accepted();
        }
        #endregion

        #region SecondO2()
        [HttpPost("secondO2/{isPressed}")]
        public async Task<ActionResult> SecondO2(bool isPressed)
        {
            var settings = await tableService.GetSettings();

            if (!settings.SabotageDeadline.HasValue)
                return Ok();

            settings.SecondO2 = isPressed;

            if (settings.FirstO2 && settings.SecondO2)
            {
                settings.SabotageDeadline = null;
                settings.FirstO2 = false;
                settings.SecondO2 = false;
                settings.SabotageCooldown = DateTime.UtcNow.AddMinutes(settings.SabotageCooldownFromMinutes);
            }

            var tableClient = tableService.GetTableClient(DbSettings.TableName);
            await tableClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            return Accepted();
        }
        #endregion

        #region GetAlivePlayers()
        [HttpGet("alive-players")]
        public async Task<ActionResult<List<DbPlayer>>> GetPlayers()
        {
            var tableClient = tableService.GetTableClient(DbPlayer.TableName);
            var players = new List<DbPlayer>();
            await foreach (var player in tableClient.QueryAsync<DbPlayer>())
                players.Add(player);
            return players.Where(p => p.IsAlive).ToList();
        }
        #endregion

        #region KickPlayer()
        [HttpPost("kick/{name}")]
        public async Task<ActionResult> KickPlayer(string name)
        {
            var player = await tableService.GetPlayer(name);
            if (player is null)
                return NotFound();

            player.IsAlive = false;

            var tableClient = tableService.GetTableClient(DbPlayer.TableName);
            await tableClient.UpdateEntityAsync(player, ETag.All, TableUpdateMode.Replace);

            if (player.Role == nameof(Role.Jester))
                await amogusService.JesterWins();
            else
                await amogusService.CheckGameAfterKill();
            
            return Accepted();
        }
        #endregion

        #region StartVoting()
        [HttpPost("start-voting")]
        public async Task<ActionResult> StartVoting()
        {
            var settings = await tableService.GetSettings();
            settings.IsVoting = true;
            var tableClient = tableService.GetTableClient(DbSettings.TableName);
            await tableClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);
            return Accepted();
        }
        #endregion

        #region EndVoting()
        [HttpPost("end-voting")]
        public async Task<ActionResult<bool>> EndVoting()
        {
            var tableClient = tableService.GetTableClient(DbSettings.TableName);
            var settings = (await tableClient.GetEntityAsync<DbSettings>("Settings", "main")).Value;

            if (settings.IsVoting)
                return Ok(false);

            var playersClient = tableService.GetTableClient(DbPlayer.TableName);
            await foreach (var player in playersClient.QueryAsync<DbPlayer>())
            {
                if (player.VotedPerson is null)
                    player.VotedPerson = "";

                await playersClient.UpdateEntityAsync(player, ETag.All, TableUpdateMode.Replace);
            }

            var result = await amogusService.CheckGameAfterVoting();

            settings = (await tableClient.GetEntityAsync<DbSettings>("Settings", "main")).Value;

            settings.IsVoting = false;

            await tableClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            return Ok(result);
        }
        #endregion
    }
}
