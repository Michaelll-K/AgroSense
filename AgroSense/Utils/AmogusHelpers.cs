using AgroSense.Entities;
using AgroSense.Enums;
using AgroSense.Hubs;
using AgroSense.Models.Player;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;

namespace AgroSense.Utils
{
    public static class AmogusHelpers
    {
        #region GetSettings()
        public static async Task<DbSettings> GetSettings(this IMongoDatabase database)
        {
            var collection = database.GetCollection<DbSettings>(DbSettings.DbName);

            return await collection
                .Find(Builders<DbSettings>.Filter.Empty)
                .FirstOrDefaultAsync();
        }
        #endregion

        #region GetPlayer()
        public static async Task<DbPlayer> GetPlayer(this IMongoDatabase database, string name, bool aliveOnly = true)
        {
            var collection = database.GetCollection<DbPlayer>(DbPlayer.DbName);

            var players = await collection
                .Find(Builders<DbPlayer>.Filter.Empty)
                .ToListAsync();

            if (aliveOnly)
                return players.FirstOrDefault(p => p.Name.ToLower() == name.ToLower() && p.IsAlive);
            else
                return players.FirstOrDefault(p => p.Name.ToLower() == name.ToLower());
        }
        #endregion

        #region ResetSettings()
        public static void ResetSettings(this DbSettings settings)
        {
            settings.IsGameActive = false;
            settings.IsPanic = false;
            settings.PanicReporter = null;
            settings.IsCorpse = false;
            settings.CorpseReporter = null;
            settings.StartDateUtc = null;
            settings.ImpostorsNames = null;
            settings.PanicCooldown = null;
            settings.SabotageStartDateUtc = null;
            settings.SabotageCooldown = null;
            settings.WinnigTeam = null;
            settings.CompletedTasksCount = 0;
            settings.IsBlackmailUsed = false;
            settings.SabotageDeadline = null;
            settings.IsDetectiveUsed = false;
        }
        #endregion

        #region SendStatusUpdate()
        public static async Task SendStatusUpdate(this IHubContext<CheckGameHub, ICheckGameClient> hub, IMongoDatabase database)
        {
            var settingsCollection = database.GetCollection<DbSettings>(DbSettings.DbName);

            var settings = await settingsCollection
                .Find(Builders<DbSettings>.Filter.Empty)
                .FirstOrDefaultAsync();

            var playersCollection = database.GetCollection<DbPlayer>(DbPlayer.DbName);

            var players = await playersCollection
                .Find(Builders<DbPlayer>.Filter.Empty)
                .ToListAsync();

            var crewmatesCount = players.Count(p => !p.Role.Contains(Role.Impostor.ToString()));

            var response = new CheckGameModel
            {
                IsGameActive = settings?.IsGameActive ?? false,
                SabotageStartDateUtc = settings?.SabotageStartDateUtc,
                ImpostorsNames = settings?.ImpostorsNames,
                IsPanic = settings?.IsPanic ?? false,
                PanicReporter = settings?.PanicReporter,
                IsCorpse = settings?.IsCorpse ?? false,
                CorpseReporter = settings?.CorpseReporter,
                SabotageDeadlineDateUtc = settings?.SabotageDeadline,
                CompletedTasks = settings?.CompletedTasksCount ?? 0,
                TasksToComplete = (settings?.TaskPerPlayer ?? 0) * crewmatesCount,
                WinningTeam = settings?.WinnigTeam,
                IsBlackmailUsed = settings?.IsBlackmailUsed ?? false,
                IsDetectiveUsed = settings?.IsDetectiveUsed ?? false,
                SabotageCooldownDateUtc = settings?.SabotageCooldown,
                PanicCooldown = settings?.PanicCooldown,
                PlayersInfo = players.Select(p => new PlayerInfo
                {
                    Name = p.Name,
                    Role = p.Role,
                    IsAlive = p.IsAlive,
                    IsBlackmailed = p.IsBlackmailed
                }).ToList()
            };

            await hub.Clients.All.AmogusStatus(response);
        }
        #endregion
    }
}
