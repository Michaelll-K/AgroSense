using Azure;
using Azure.Data.Tables;
using AgroSense.Entities;
using AgroSense.Enums;
using AgroSense.Hubs;
using AgroSense.Models.Player;
using Microsoft.AspNetCore.SignalR;

namespace AgroSense.Utils
{
    public static class AmogusHelpers
    {
        #region GetSettings()
        public static async Task<DbSettings> GetSettings(this TableServiceClient tableService)
        {
            var tableClient = tableService.GetTableClient(DbSettings.TableName);
            try
            {
                return (await tableClient.GetEntityAsync<DbSettings>("Settings", "main")).Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return new DbSettings();
            }
        }
        #endregion

        #region GetPlayer()
        public static async Task<DbPlayer?> GetPlayer(this TableServiceClient tableService, string name, bool aliveOnly = true)
        {
            var tableClient = tableService.GetTableClient(DbPlayer.TableName);
            var players = new List<DbPlayer>();
            await foreach (var player in tableClient.QueryAsync<DbPlayer>())
                players.Add(player);

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
            settings.IsSniperUsed = false;
            settings.IsSheriffUsed = false;
        }
        #endregion

        #region SendStatusUpdate()
        public static async Task SendStatusUpdate(this IHubContext<CheckGameHub, ICheckGameClient> hub, TableServiceClient tableService)
        {
            DbSettings? settings = null;
            try
            {
                var settingsClient = tableService.GetTableClient(DbSettings.TableName);
                settings = (await settingsClient.GetEntityAsync<DbSettings>("Settings", "main")).Value;
            }
            catch (RequestFailedException) { }

            var playersClient = tableService.GetTableClient(DbPlayer.TableName);
            var players = new List<DbPlayer>();
            await foreach (var player in playersClient.QueryAsync<DbPlayer>())
                players.Add(player);

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
                TasksToComplete = (settings?.TasksPerPlayer ?? 0) * crewmatesCount,
                WinningTeam = settings?.WinnigTeam,
                IsBlackmailUsed = settings?.IsBlackmailUsed ?? false,
                IsDetectiveUsed = settings?.IsDetectiveUsed ?? false,
                IsSniperUsed = settings?.IsSniperUsed ?? false,
                IsSheriffUsed = settings?.IsSheriffUsed ?? false,
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
