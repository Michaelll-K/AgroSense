using AgroSense.Entities;
using AgroSense.Enums;
using AgroSense.Models.Player;
using AgroSense.Utils;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Data;

namespace AgroSense.Controllers
{
    [ApiController]
    [Route("api/impostor")]
    public class ImpostorController : ControllerBase
    {
        private readonly IMongoDatabase database;

        #region ImpostorController()
        public ImpostorController(IMongoDatabase database)
        {
            this.database = database;
        }
        #endregion

        #region Blackmail()
        [HttpPost("{name}/blackmail/{blackmailName}")]
        public async Task<ActionResult> Blackmail(string name, string blackmailName)
        {
            var settings = await database.GetSettings();

            var currentPlayer = await database.GetPlayer(name);

            var blackmailPlayer = await database.GetPlayer(blackmailName);

            if (currentPlayer is null || blackmailPlayer is null || !currentPlayer.Role.Contains(Role.Impostor.ToString()))
                return NotFound();

            if (settings.IsBlackmailUsed)
                return BadRequest("Blackmail został już wykorzystany!");

            blackmailPlayer.IsBlackmailed = true;

            var playersCollection = database.GetCollection<DbPlayer>(DbPlayer.DbName);

            await playersCollection.ReplaceOneAsync(
                Builders<DbPlayer>.Filter.Eq(s => s.Id, blackmailPlayer.Id),
                blackmailPlayer
            );

            settings.IsBlackmailUsed = true;

            var settingsCollection = database.GetCollection<DbSettings>(DbSettings.DbName);

            await settingsCollection.ReplaceOneAsync(
                Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                settings
            );

            return Accepted();
        }
        #endregion

        #region Sabotage()
        [HttpPost("{name}/sabotage/{delay}")]
        public async Task<ActionResult> Sabotage(string name, int delay)
        {
            var settings = await database.GetSettings();

            var currentPlayer = await database.GetPlayer(name);

            if (currentPlayer is null || !currentPlayer.Role.Contains(Role.Impostor.ToString()))
                return NotFound();

            if (settings.SabotageCooldown > DateTime.UtcNow)
                return BadRequest("Sabotaż jeszcze się nie odnowił");

            settings.SabotageStartDateUtc = DateTime.UtcNow.AddMinutes(delay);
            settings.SabotageDeadline = settings.SabotageStartDateUtc.Value.AddMinutes(settings.SabotageDeadlineFromMinutes);

            settings.SabotageCooldown = settings.SabotageDeadline;

            var settingsCollection = database.GetCollection<DbSettings>(DbSettings.DbName);

            await settingsCollection.ReplaceOneAsync(
                Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                settings
            );

            return Accepted();
        }
        #endregion

        #region GetUsersToBlackmail()
        [HttpGet("users-to-blackmail")]
        public async Task<ActionResult<List<DbPlayer>>> GetUsersToBlackmail()
        {
            var collection = database.GetCollection<DbPlayer>(DbPlayer.DbName);

            var players = await collection
                .Find(Builders<DbPlayer>.Filter.Empty)
                .ToListAsync();

            return players.Where(p => p.IsAlive).ToList();
        }
        #endregion
    }
}
