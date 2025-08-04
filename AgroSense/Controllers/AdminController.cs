using AgroSense.Entities;
using AgroSense.Enums;
using AgroSense.Models;
using AgroSense.Models.Admin;
using AgroSense.Services;
using AgroSense.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
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
        private readonly IMongoDatabase database;
        private readonly IConfiguration configuration;
        private readonly AmogusService startService;

        #region AdminController()
        public AdminController(IMongoDatabase database, IConfiguration configuration, AmogusService tasksService)
        {
            this.database = database;
            this.configuration = configuration;
            this.startService = tasksService;
        }
        #endregion

        #region Login()
        [HttpPost("login")]
        public ActionResult<string> Login([FromBody] AuthModel model)
        {
            if (model.Username != configuration["Admin:Login"] || model.Password != configuration["Admin:Password"])
            {
                return BadRequest("Błędne dane logowania");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, model.Username)
            };

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
            return await database.GetSettings();
        }
        #endregion

        #region SaveSettings()
        [Authorize]
        [HttpPost("settings")]
        public async Task<ActionResult> SaveSettings([FromBody] SettingsModel model)
        {
            var collection = database.GetCollection<DbSettings>(DbSettings.DbName);

            var settings = await collection
                .Find(Builders<DbSettings>.Filter.Empty)
                .FirstOrDefaultAsync();

            settings ??= new DbSettings
            {
                IsGameActive = false
            };

            settings.ResetSettings();

            settings.TaskPerPlayer = model.TaskPerPlayer;
            settings.SabotageDeadlineFromMinutes = model.SabotageDeadlineFromMinutes;
            settings.ImpostorsAmount = model.ImpostorsAmount;
            settings.DetectivesAmount = model.DetectivesAmount;
            settings.DoctorsAmount = model.DoctorsAmount;
            settings.IsPanic = false;
            settings.PanicCooldown = DateTime.UtcNow;
            settings.SabotageCooldown = DateTime.UtcNow;
            settings.PanicCooldownFromMinutes = model.PanicCooldownFromMinutes;
            settings.SabotageCooldownFromMinutes = model.SabotageCooldownFromMinutes;

            if (string.IsNullOrEmpty(settings.Id))
            {
                await collection.InsertOneAsync(settings);
            }
            else
            {
                await collection.ReplaceOneAsync(
                    Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                    settings
                );
            }

            return Ok();
        }
        #endregion

        #region GetPlayers()
        [Authorize]
        [HttpGet("players")]
        public async Task<ActionResult<List<DbPlayer>>> GetPlayers()
        {
            var collection = database.GetCollection<DbPlayer>(DbPlayer.DbName);

            var players = await collection
                .Find(Builders<DbPlayer>.Filter.Empty)
                .ToListAsync();

            return players;
        }
        #endregion

        #region ResetPlayers()
        [Authorize]
        [HttpPost("reset-players")]
        public async Task<ActionResult> ResetPlayers()
        {
            var collection = database.GetCollection<DbPlayer>(DbPlayer.DbName);

            var players = await collection
                .Find(Builders<DbPlayer>.Filter.Empty)
                .ToListAsync();

            foreach (var player in players)
            {
                var objectId = ObjectId.Parse(player.Id);

                await collection.DeleteOneAsync(
                    Builders<DbPlayer>.Filter.Eq("_id", objectId)
                );
            }

            return Accepted();
        }
        #endregion

        #region StartGame()
        [Authorize]
        [HttpPost("start")]
        public async Task<ActionResult> StartGame()
        {
            // Pobieranie ustawień
            var settingsCollection = database.GetCollection<DbSettings>(DbSettings.DbName);

            var settings = await settingsCollection
                .Find(Builders<DbSettings>.Filter.Empty)
                .FirstOrDefaultAsync();

            if (settings is null)
                return NotFound();

            // Pobranie graczy
            var playersCollection = database.GetCollection<DbPlayer>(DbPlayer.DbName);

            var players = await playersCollection
                .Find(Builders<DbPlayer>.Filter.Empty)
                .ToListAsync();

            if (players.Count < settings.ImpostorsAmount + settings.DetectivesAmount + settings.DoctorsAmount)
                return BadRequest("Zbyt wiele ról na taką ilośc graczy!");

            // Przypisywanie ról
            startService.DetermineRoles(settings, players);

            // Zapis zmian w graczach
            foreach (var player in players)
            {
                var playerTasks = await startService.GetTasksForPlayer(settings, players.Count);

                player.TasksJson = JsonSerializer.Serialize(playerTasks);

                player.IsAlive = true;
                player.IsBlackmailed = false;

                await playersCollection.ReplaceOneAsync(
                    Builders<DbPlayer>.Filter.Eq(s => s.Id, player.Id),
                    player
                );
            }

            // Start i zapis ustawień
            settings.ResetSettings();

            settings.IsGameActive = true;
            settings.StartDateUtc = DateTime.UtcNow.AddMinutes(1);
            settings.ImpostorsNames = string.Join(", ", players.Where(p => p.Role.Contains(Role.Impostor.ToString())).Select(p => p.Name));

            await settingsCollection.ReplaceOneAsync(
                Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                settings
            );

            return Ok();
        }
        #endregion

        #region StopGame()
        [Authorize]
        [HttpPost("stop")]
        public async Task<ActionResult> StopGame()
        {
            var collection = database.GetCollection<DbSettings>(DbSettings.DbName);

            var settings = await collection
                .Find(Builders<DbSettings>.Filter.Empty)
                .FirstOrDefaultAsync();

            if (settings is null)
                return NotFound();

            settings.ResetSettings();

            await collection.ReplaceOneAsync(
                Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                settings
            );

            return Ok();
        }
        #endregion

        #region GetTasks()
        [Authorize]
        [HttpGet("tasks")]
        public async Task<ActionResult<List<DbTask>>> GetTasks()
        {
            var collection = database.GetCollection<DbTask>(DbTask.DbName);

            var tasks = await collection
                .Find(Builders<DbTask>.Filter.Empty)
                .ToListAsync();

            return Ok(tasks);
        }
        #endregion

        #region CreateTask()
        [Authorize]
        [HttpPost("task")]
        public async Task<ActionResult> CreateTask([FromBody] TaskModel model)
        {
            var collection = database.GetCollection<DbTask>(DbTask.DbName);

            var task = new DbTask
            {
                Id = "",
                Name = model.Name,
                Location = model.Location,
                Description = model.Description
            };

            await collection.InsertOneAsync(task);

            return Ok();
        }
        #endregion

        #region DeleteTask()
        [Authorize]
        [HttpDelete("task/{id}")]
        public async Task<ActionResult> DeleteTask(string id)
        {
            var collection = database.GetCollection<DbTask>(DbTask.DbName);

            var objectId = ObjectId.Parse(id);

            await collection.DeleteOneAsync(
                Builders<DbTask>.Filter.Eq("_id", objectId)
            );

            return Accepted();
        }
        #endregion
    }
}
