using AgroSense.Entities;
using AgroSense.Enums;
using AgroSense.Models.Admin;
using AgroSense.Models.Player;
using AgroSense.Services;
using AgroSense.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgroSense.Controllers
{
    [ApiController]
    [Route("api/player")]
    public class PlayerController : ControllerBase
    {
        private readonly IMongoDatabase database;
        private readonly AmogusService amogusService;

        #region PlayerController()
        public PlayerController(IMongoDatabase database, AmogusService amogusService)
        {
            this.database = database;
            this.amogusService = amogusService;
        }
        #endregion

        #region CheckGame()
        [HttpGet("check-game")]
        public async Task<ActionResult> CheckGame()
        {
            return Ok();
        }
        #endregion

        #region SignIn()
        [HttpPost("{name}/sign-in")]
        public async Task<ActionResult<string>> SignIn(string name)
        {
            var collection = database.GetCollection<DbPlayer>(DbPlayer.DbName);

            var players = await collection
                .Find(Builders<DbPlayer>.Filter.Empty)
                .ToListAsync();

            var settings = await database.GetSettings();

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
                Name = name,
                IsAlive = true,
                Role = Role.Crewmate.ToString()
            };

            await collection.InsertOneAsync(newPlayer);

            return Accepted();
        }
        #endregion

        #region GetTasks()
        [HttpGet("{name}/tasks")]
        public async Task<ActionResult<List<TaskModel>>> GetTasks(string name)
        {
            var settings = await database.GetSettings();

            var currentPlayer = await database.GetPlayer(name, false);

            if (!settings.IsGameActive || currentPlayer is null || string.IsNullOrEmpty(currentPlayer.TasksJson))
                return NotFound();

            return JsonSerializer.Deserialize<List<TaskModel>>(currentPlayer.TasksJson);
        }
        #endregion

        #region StartCorpse()
        [HttpPost("{name}/corpse")]
        public async Task<ActionResult> StartCorpse(string name)
        {
            var settings = await database.GetSettings();

            var currentPlayer = await database.GetPlayer(name);

            if (!settings.IsGameActive || currentPlayer is null)
                return NotFound();

            settings.IsCorpse = true;
            settings.CorpseReporter = name;

            var collection = database.GetCollection<DbSettings>(DbSettings.DbName);

            await collection.ReplaceOneAsync(
                Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                settings
            );

            return Accepted();
        }
        #endregion

        #region UseDetective()
        [HttpPost("{name}/use-detective/{checkPlayerName}")]
        public async Task<ActionResult<string>> UseDetective(string name, string checkPlayerName)
        {
            var settings = await database.GetSettings();

            var currentPlayer = await database.GetPlayer(name);

            var playerToCheck = await database.GetPlayer(checkPlayerName);

            if (!settings.IsGameActive || currentPlayer is null || playerToCheck is null)
                return NotFound();

            if (currentPlayer.Role != nameof(Role.Detective))
                return Ok();

            settings.IsDetectiveUsed = true;

            var collection = database.GetCollection<DbSettings>(DbSettings.DbName);

            await collection.ReplaceOneAsync(
                Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                settings
            );

            return playerToCheck.Role;
        }
        #endregion

        #region CompleteTask()
        [HttpPost("{name}/complete-task/{id}")]
        public async Task<ActionResult> CompleteTask(string name, string id)
        {
            var settings = await database.GetSettings();

            var currentPlayer = await database.GetPlayer(name, false);

            if (!settings.IsGameActive || currentPlayer is null || string.IsNullOrEmpty(currentPlayer.TasksJson))
                return NotFound();

            var playerTasks = JsonSerializer.Deserialize<List<TaskModel>>(currentPlayer.TasksJson);

            var taskToComplete = playerTasks.FirstOrDefault(t => t.Id == id);

            if (taskToComplete is null)
                return NotFound();

            taskToComplete.IsCompleted = true;

            currentPlayer.TasksJson = JsonSerializer.Serialize(playerTasks);

            var collection = database.GetCollection<DbPlayer>(DbPlayer.DbName);

            await collection.ReplaceOneAsync(
                Builders<DbPlayer>.Filter.Eq(p => p.Id, currentPlayer.Id),
                currentPlayer
            );

            if (currentPlayer.Role.Contains(nameof(Role.Impostor)))
                return Ok();

            // Dodawanie wszytskich tasków
            var playersCollection = database.GetCollection<DbPlayer>(DbPlayer.DbName);

            var players = await playersCollection
                .Find(Builders<DbPlayer>.Filter.Empty)
                .ToListAsync();

            settings.CompletedTasksCount = players.SelectMany(p => JsonSerializer.Deserialize<List<TaskModel>>(p.TasksJson)).Count(t => t.IsCompleted);

            var settingsCollection = database.GetCollection<DbSettings>(DbSettings.DbName);

            await settingsCollection.ReplaceOneAsync(
                Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                settings
            );

            await amogusService.CheckGameAfterTask();

            return Accepted();
        }
        #endregion

        #region KillPlayer()
        [HttpPost("{name}/kill")]
        public async Task<ActionResult> KillPlayer(string name)
        {
            var player = await database.GetPlayer(name, false);

            if (player is null)
                return NotFound();

            player.IsAlive = !player.IsAlive;

            var collection = database.GetCollection<DbPlayer>(DbPlayer.DbName);

            await collection.ReplaceOneAsync(
                Builders<DbPlayer>.Filter.Eq(p => p.Id, player.Id),
                player
            );

            await amogusService.CheckGameAfterKill();

            return Accepted();
        }
        #endregion
    }
}
