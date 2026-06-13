using Azure;
using Azure.Data.Tables;
using AgroSense.Entities;
using AgroSense.Enums;
using AgroSense.Models.Admin;
using AgroSense.Services;
using AgroSense.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace AgroSense.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly TableServiceClient tableService;
        private readonly IConfiguration configuration;
        private readonly AmogusService startService;

        #region AdminController()
        public AdminController(TableServiceClient tableService, IConfiguration configuration, AmogusService startService)
        {
            this.tableService = tableService;
            this.configuration = configuration;
            this.startService = startService;
        }
        #endregion

        #region Login()
        [HttpPost("login")]
        public ActionResult<string> Login([FromBody] AuthModel model)
        {
            if (model.Username != configuration["Admin:Login"] || model.Password != configuration["Admin:Password"])
                return BadRequest("Błędne dane logowania");

            var claims = new[] { new Claim(ClaimTypes.Name, model.Username) };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: configuration["Jwt:Issuer"],
                audience: configuration["Jwt:Issuer"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        #endregion

        #region GetSettings()
        [Authorize]
        [HttpGet("settings")]
        public async Task<ActionResult<DbSettings>> GetSettings()
        {
            return await tableService.GetSettings();
        }
        #endregion

        #region SaveSettings()
        [Authorize]
        [HttpPost("settings")]
        public async Task<ActionResult> SaveSettings([FromBody] SettingsModel model)
        {
            var tableClient = tableService.GetTableClient(DbSettings.TableName);

            DbSettings settings;
            try
            {
                settings = (await tableClient.GetEntityAsync<DbSettings>("Settings", "main")).Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                settings = new DbSettings { IsGameActive = false };
            }

            settings.ResetSettings();

            settings.ShortTasksPerPlayer = model.ShortTasksPerPlayer;
            settings.LongTasksPerPlayer = model.LongTasksPerPlayer;
            settings.SabotageDeadlineFromMinutes = model.SabotageDeadlineFromMinutes;
            settings.ImpostorsAmount = model.ImpostorsAmount;
            settings.DetectivesAmount = model.DetectivesAmount;
            settings.DoctorsAmount = model.DoctorsAmount;
            settings.IsPanic = false;
            settings.PanicCooldown = DateTime.UtcNow;
            settings.SabotageCooldown = DateTime.UtcNow;
            settings.PanicCooldownFromMinutes = model.PanicCooldownFromMinutes;
            settings.SabotageCooldownFromMinutes = model.SabotageCooldownFromMinutes;

            await tableClient.UpsertEntityAsync(settings, TableUpdateMode.Replace);

            return Ok();
        }
        #endregion

        #region GetPlayers()
        [Authorize]
        [HttpGet("players")]
        public async Task<ActionResult<List<DbPlayer>>> GetPlayers()
        {
            var tableClient = tableService.GetTableClient(DbPlayer.TableName);
            var players = new List<DbPlayer>();
            await foreach (var player in tableClient.QueryAsync<DbPlayer>())
                players.Add(player);
            return players;
        }
        #endregion

        #region ResetPlayers()
        [Authorize]
        [HttpPost("reset-players")]
        public async Task<ActionResult> ResetPlayers()
        {
            var tableClient = tableService.GetTableClient(DbPlayer.TableName);
            var players = new List<DbPlayer>();
            await foreach (var player in tableClient.QueryAsync<DbPlayer>())
                players.Add(player);

            foreach (var player in players)
                await tableClient.DeleteEntityAsync(player.PartitionKey, player.RowKey, ETag.All);

            return Accepted();
        }
        #endregion

        #region StartGame()
        [Authorize]
        [HttpPost("start")]
        public async Task<ActionResult> StartGame()
        {
            var settingsClient = tableService.GetTableClient(DbSettings.TableName);
            DbSettings settings;
            try
            {
                settings = (await settingsClient.GetEntityAsync<DbSettings>("Settings", "main")).Value;
            }
            catch (RequestFailedException)
            {
                return NotFound();
            }

            var playersClient = tableService.GetTableClient(DbPlayer.TableName);
            var players = new List<DbPlayer>();
            await foreach (var player in playersClient.QueryAsync<DbPlayer>())
                players.Add(player);

            if (players.Count < settings.ImpostorsAmount + settings.DetectivesAmount + settings.DoctorsAmount)
                return BadRequest("Zbyt wiele ról na taką ilośc graczy!");

            startService.DetermineRoles(settings, players);

            foreach (var player in players)
            {
                var playerTasks = await startService.GetTasksForPlayer(settings, players.Count);
                player.TasksJson = JsonSerializer.Serialize(playerTasks);
                player.IsAlive = true;
                player.IsBlackmailed = false;
                await playersClient.UpdateEntityAsync(player, ETag.All, TableUpdateMode.Replace);
            }

            settings.ResetSettings();
            settings.IsGameActive = true;
            settings.StartDateUtc = DateTime.UtcNow.AddMinutes(1);
            settings.ImpostorsNames = string.Join(", ", players.Where(p => p.Role.Contains(Role.Impostor.ToString())).Select(p => p.Name));

            await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            return Ok();
        }
        #endregion

        #region StopGame()
        [Authorize]
        [HttpPost("stop")]
        public async Task<ActionResult> StopGame()
        {
            var tableClient = tableService.GetTableClient(DbSettings.TableName);
            DbSettings settings;
            try
            {
                settings = (await tableClient.GetEntityAsync<DbSettings>("Settings", "main")).Value;
            }
            catch (RequestFailedException)
            {
                return NotFound();
            }

            settings.ResetSettings();
            await tableClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            return Ok();
        }
        #endregion

        #region GetTasks()
        [Authorize]
        [HttpGet("tasks")]
        public async Task<ActionResult<List<DbTask>>> GetTasks()
        {
            var tableClient = tableService.GetTableClient(DbTask.TableName);
            var tasks = new List<DbTask>();
            await foreach (var task in tableClient.QueryAsync<DbTask>())
                tasks.Add(task);
            return Ok(tasks);
        }
        #endregion

        #region CreateTask()
        [Authorize]
        [HttpPost("task")]
        public async Task<ActionResult> CreateTask([FromBody] TaskModel model)
        {
            var tableClient = tableService.GetTableClient(DbTask.TableName);

            var task = new DbTask
            {
                RowKey = Guid.NewGuid().ToString(),
                Name = model.Name,
                Location = model.Location,
                Description = model.Description,
                TaskLength = model.TaskLength
            };

            await tableClient.AddEntityAsync(task);

            return Ok();
        }
        #endregion

        #region DeleteTask()
        [Authorize]
        [HttpDelete("task/{id}")]
        public async Task<ActionResult> DeleteTask(string id)
        {
            var tableClient = tableService.GetTableClient(DbTask.TableName);
            await tableClient.DeleteEntityAsync("Task", id, ETag.All);
            return Accepted();
        }
        #endregion
    }
}
