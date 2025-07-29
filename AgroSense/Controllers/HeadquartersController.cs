using AgroSense.Entities;
using AgroSense.Enums;
using AgroSense.Models.Player;
using AgroSense.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace AgroSense.Controllers
{
    [ApiController]
    [Route("api/hq")]
    public class HeadquartersController : ControllerBase
    {
        private readonly IMongoDatabase database;

        #region HeadquartersController()
        public HeadquartersController(IMongoDatabase database)
        {
            this.database = database;
        }
        #endregion

        #region StartPanic()
        [HttpPost("{name}/start-panic")]
        public async Task<ActionResult<DateTime>> StartPanic(string name)
        {
            var player = await database.GetPlayer(name);

            if (player is null)
                return NotFound();

            var collection = database.GetCollection<DbSettings>(DbSettings.DbName);

            var settings = await collection
                .Find(Builders<DbSettings>.Filter.Empty)
                .FirstOrDefaultAsync();

            if (settings.PanicCooldown > DateTime.UtcNow)
                return BadRequest("Przycisk paniki jeszcze się nie odnowił!");

            settings.IsPanic = true;
            settings.PanicReporter = name;

            await collection.ReplaceOneAsync(
                Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                settings
            );

            return Accepted();
        }
        #endregion

        #region EndPanic()
        [HttpPost("end-panic")]
        public async Task<ActionResult<DateTime>> EndPanic()
        {
            var collection = database.GetCollection<DbSettings>(DbSettings.DbName);

            var settings = await collection
                .Find(Builders<DbSettings>.Filter.Empty)
                .FirstOrDefaultAsync();

            settings.IsPanic = false;
            settings.PanicCooldown = DateTime.UtcNow.AddMinutes(settings.PanicCooldownFromMinutes);
            settings.IsCorpse = false;
            settings.IsBlackmailUsed = false;

            await collection.ReplaceOneAsync(
                Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                settings
            );

            var playersCollection = database.GetCollection<DbPlayer>(DbPlayer.DbName);

            var players = await playersCollection
                .Find(Builders<DbPlayer>.Filter.Empty)
                .ToListAsync();

            foreach (var player in players)
            {
                player.IsBlackmailed = false;

                await playersCollection.ReplaceOneAsync(
                    Builders<DbPlayer>.Filter.Eq(s => s.Id, player.Id),
                    player
                );
            }

            return Accepted();
        }
        #endregion

        #region CheckSabotage()
        [HttpGet("check-sabotage")]
        public async Task<ActionResult> CheckSabotage()
        {
            var settings = await database.GetSettings();

            if (settings.SabotageDeadline <= DateTime.UtcNow)
            {
                settings.IsGameActive = false;
                settings.WinnigTeam = Role.Impostor.ToString();

                var collection = database.GetCollection<DbSettings>(DbSettings.DbName);

                await collection.ReplaceOneAsync(
                    Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                    settings
                );
            }

            return Ok();
        }
        #endregion

        #region FirstO2()
        [HttpPost("firstO2/{isPressed}")]
        public async Task<ActionResult> FirstO2(bool isPressed)
        {
            var settings = await database.GetSettings();

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

            var collection = database.GetCollection<DbSettings>(DbSettings.DbName);

            await collection.ReplaceOneAsync(
                Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                settings
            );

            return Accepted();
        }
        #endregion

        #region SecondO2()
        [HttpPost("secondO2/{isPressed}")]
        public async Task<ActionResult> SecondO2(bool isPressed)
        {
            var settings = await database.GetSettings();

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

            var collection = database.GetCollection<DbSettings>(DbSettings.DbName);

            await collection.ReplaceOneAsync(
                Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                settings
            );

            return Accepted();
        }
        #endregion

        #region GetAlivePlayers()
        [HttpGet("alive-players")]
        public async Task<ActionResult<List<DbPlayer>>> GetPlayers()
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
