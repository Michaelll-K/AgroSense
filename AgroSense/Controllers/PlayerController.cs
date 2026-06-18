using AgroSense.Entities;
using AgroSense.Enums;
using AgroSense.Models.Admin;
using AgroSense.Services;
using AgroSense.Utils;
using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using System.Numerics;
using System.Text.Json;

namespace AgroSense.Controllers
{
    [ApiController]
    [Route("api/player")]
    public class PlayerController : ControllerBase
    {
        private readonly TableServiceClient tableService;
        private readonly AmogusService amogusService;

        #region PlayerController()
        public PlayerController(TableServiceClient tableService, AmogusService amogusService)
        {
            this.tableService = tableService;
            this.amogusService = amogusService;
        }
        #endregion

        #region CheckGame()
        [HttpGet("check-game")]
        public ActionResult CheckGame() => Ok();
        #endregion

        #region SignIn()
        [HttpPost("{name}/sign-in")]
        public async Task<ActionResult<string>> SignIn(string name)
        {
            var tableClient = tableService.GetTableClient(DbPlayer.TableName);
            var players = new List<DbPlayer>();
            await foreach (var player in tableClient.QueryAsync<DbPlayer>())
                players.Add(player);

            var settings = await tableService.GetSettings();
            var currentPlayer = players.FirstOrDefault(p => p.Name.ToLower() == name.ToLower());

            if (settings.IsGameActive)
            {
                if (currentPlayer is null)
                    return BadRequest("Nie ma takiego gracza!");
                return Accepted();
            }
            else if (currentPlayer is not null)
            {
                return BadRequest("Taki gracz już istnieje!");
            }

            var newPlayer = new DbPlayer
            {
                RowKey = Guid.NewGuid().ToString(),
                Name = name,
                IsAlive = true,
                Role = Role.Crewmate.ToString()
            };

            await tableClient.AddEntityAsync(newPlayer);

            return Accepted();
        }
        #endregion

        #region GetTasks()
        [HttpGet("{name}/tasks")]
        public async Task<ActionResult<List<TaskModel>>> GetTasks(string name)
        {
            var settings = await tableService.GetSettings();
            var currentPlayer = await tableService.GetPlayer(name, false);

            if (!settings.IsGameActive || currentPlayer is null || string.IsNullOrEmpty(currentPlayer.TasksJson))
                return NotFound();

            return JsonSerializer.Deserialize<List<TaskModel>>(currentPlayer.TasksJson);
        }
        #endregion

        #region StartCorpse()
        [HttpPost("{name}/corpse")]
        public async Task<ActionResult> StartCorpse(string name)
        {
            var settings = await tableService.GetSettings();
            var currentPlayer = await tableService.GetPlayer(name);

            if (!settings.IsGameActive || currentPlayer is null)
                return NotFound();

            settings.IsCorpse = true;
            settings.CorpseReporter = name;

            var tableClient = tableService.GetTableClient(DbSettings.TableName);
            await tableClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            return Accepted();
        }
        #endregion

        #region UseDetective()
        [HttpPost("{name}/use-detective/{checkPlayerName}")]
        public async Task<ActionResult<string>> UseDetective(string name, string checkPlayerName)
        {
            var settings = await tableService.GetSettings();
            var currentPlayer = await tableService.GetPlayer(name);
            var playerToCheck = await tableService.GetPlayer(checkPlayerName);

            if (!settings.IsGameActive || currentPlayer is null || playerToCheck is null)
                return NotFound();

            if (currentPlayer.Role != nameof(Role.Detective))
                return Ok();

            settings.IsDetectiveUsed = true;

            var tableClient = tableService.GetTableClient(DbSettings.TableName);
            await tableClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            return playerToCheck.Role;
        }
        #endregion

        #region UseSniper()
        [HttpPost("{name}/use-sniper/{shotPlayerName}")]
        public async Task<ActionResult<bool>> UseSniper(string name, string shotPlayerName, string guesedRole)
        {
            var settings = await tableService.GetSettings();
            var currentPlayer = await tableService.GetPlayer(name);
            var playerToShoot = await tableService.GetPlayer(shotPlayerName);

            if (!settings.IsGameActive || currentPlayer is null || playerToShoot is null)
                return NotFound();

            if (currentPlayer.Role != nameof(Role.ImpostorSniper))
                return Ok();


            var result = true;

            if (guesedRole == playerToShoot.Role)
                await KillPlayer(playerToShoot.Name);
            else
            {
                await KillPlayer(currentPlayer.Name);
                result = false;
            }

            settings = await tableService.GetSettings();
            settings.IsSniperUsed = true;

            var tableClient = tableService.GetTableClient(DbSettings.TableName);
            await tableClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            return Ok(result);
        }
        #endregion

        #region UseSheriff()
        [HttpPost("{name}/use-sheriff/{shotPlayerName}")]
        public async Task<ActionResult<bool>> UseSheriff(string name, string shotPlayerName)
        {
            var settings = await tableService.GetSettings();
            var currentPlayer = await tableService.GetPlayer(name);
            var playerToShoot = await tableService.GetPlayer(shotPlayerName);

            if (!settings.IsGameActive || currentPlayer is null || playerToShoot is null)
                return NotFound();

            if (currentPlayer.Role != nameof(Role.Sheriff))
                return Ok();

            var result = true;

            if (playerToShoot.Role.Contains(nameof(Role.Impostor)))
                await KillPlayer(playerToShoot.Name);
            else
            {
                await KillPlayer(currentPlayer.Name);
                result = false;
            }

            settings = await tableService.GetSettings();
            settings.IsSheriffUsed = true;

            var tableClient = tableService.GetTableClient(DbSettings.TableName);
            await tableClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            return Ok(result);
        }
        #endregion

        #region CompleteTask()
        [HttpPost("{name}/complete-task/{id}")]
        public async Task<ActionResult> CompleteTask(string name, string id)
        {
            var settings = await tableService.GetSettings();
            var currentPlayer = await tableService.GetPlayer(name, false);

            if (!settings.IsGameActive || currentPlayer is null || string.IsNullOrEmpty(currentPlayer.TasksJson))
                return NotFound();

            var playerTasks = JsonSerializer.Deserialize<List<TaskModel>>(currentPlayer.TasksJson);
            var taskToComplete = playerTasks!.FirstOrDefault(t => t.Id == id);

            if (taskToComplete is null)
                return NotFound();

            taskToComplete.IsCompleted = true;
            currentPlayer.TasksJson = JsonSerializer.Serialize(playerTasks);

            var playersClient = tableService.GetTableClient(DbPlayer.TableName);
            await playersClient.UpdateEntityAsync(currentPlayer, ETag.All, TableUpdateMode.Replace);

            if (currentPlayer.Role.Contains(nameof(Role.Impostor)))
                return Ok();

            var players = new List<DbPlayer>();
            await foreach (var player in playersClient.QueryAsync<DbPlayer>())
                players.Add(player);

            settings.CompletedTasksCount = players
                .Where(p => !string.IsNullOrEmpty(p.TasksJson))
                .SelectMany(p => JsonSerializer.Deserialize<List<TaskModel>>(p.TasksJson)!)
                .Count(t => t.IsCompleted);

            var settingsClient = tableService.GetTableClient(DbSettings.TableName);
            await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            await amogusService.CheckGameAfterTask();

            return Accepted();
        }
        #endregion

        #region KillPlayer()
        [HttpPost("{name}/kill")]
        public async Task<ActionResult> KillPlayer(string name)
        {
            var player = await tableService.GetPlayer(name, false);

            if (player is null)
                return NotFound();

            player.IsAlive = !player.IsAlive;

            var tableClient = tableService.GetTableClient(DbPlayer.TableName);
            await tableClient.UpdateEntityAsync(player, ETag.All, TableUpdateMode.Replace);

            await amogusService.CheckGameAfterKill();

            return Accepted();
        }
        #endregion

        #region VotePlayer()
        [HttpPost("{name}/vote/{votedPlayerName}")]
        public async Task<ActionResult<bool?>> VotePlayer(string name, string votedPlayerName)
        {
            var settings = await tableService.GetSettings();
            var currentPlayer = await tableService.GetPlayer(name);
            var votedPlayer = await tableService.GetPlayer(votedPlayerName);
            if (!settings.IsGameActive || currentPlayer is null || votedPlayer is null)
                return NotFound();

            if (!settings.IsVoting)
                return BadRequest("Nie trwa głosowanie!");

            currentPlayer.VotedPerson = votedPlayer.Name;

            var tableClient = tableService.GetTableClient(DbPlayer.TableName);
            await tableClient.UpdateEntityAsync(currentPlayer, ETag.All, TableUpdateMode.Replace);

            var players = tableClient.Query<DbPlayer>().AsEnumerable();
            if (players.All(p => p.VotedPerson != null))
                Ok(await amogusService.CheckGameAfterVoting());

            return Accepted();
        }
        #endregion
    }
}
