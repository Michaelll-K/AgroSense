using AgroSense.Entities;
using AgroSense.Enums;
using AgroSense.Models.Admin;
using AgroSense.Utils;
using Azure;
using Azure.Data.Tables;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace AgroSense.Services
{
    public class AmogusService
    {
        private static readonly Random random = new Random();

        private readonly TableServiceClient tableService;

        #region AmogusService()
        public AmogusService(TableServiceClient tableService)
        {
            this.tableService = tableService;
        }
        #endregion

        #region GetTasksForPlayer()
        public async Task<List<TaskModel>> GetTasksForPlayer(DbSettings settings, int playersCount)
        {
            var tableClient = tableService.GetTableClient(DbTask.TableName);

            var tasks = new List<DbTask>();
            await foreach (var task in tableClient.QueryAsync<DbTask>())
                tasks.Add(task);

            if (settings.TasksPerPlayer > tasks.Count)
                throw new ArgumentException("Żądana liczba elementów jest większa niż liczba dostępnych elementów.");

            var shortTasks = tasks
                .Where(t => t.TaskLength == TaskLength.Short)
                .OrderBy(t => random.Next())
                .Take(settings.ShortTasksPerPlayer)
                .Select(t => new TaskModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    Location = t.Location,
                    Description = t.Description,
                    IsCompleted = false
                })
                .ToList();

            var longTasks = tasks
                .Where(t => t.TaskLength == TaskLength.Long)
                .OrderBy(t => random.Next())
                .Take(settings.ShortTasksPerPlayer)
                .Select(t => new TaskModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    Location = t.Location,
                    Description = t.Description,
                    IsCompleted = false
                })
                .ToList();

            shortTasks.AddRange(longTasks);

            return shortTasks.OrderBy(t => random.Next()).ToList();
        }
        #endregion

        #region DetermineRoles()
        public void DetermineRoles(DbSettings settings, List<DbPlayer> players)
        {
            foreach (var player in players)
                player.Role = nameof(Role.Crewmate);

            Shuffle(players);

            // Specjalne role impostora — każda może wystąpić co najwyżej raz
            var specialImpostorRoles = new List<(string Role, int Chance)>
            {
                (nameof(Role.ImpostorBlackmailer), settings.ImpostorBlackmailerChance),
                (nameof(Role.ImpostorSniper),      settings.ImpostorSniperChance),
            };
            var usedImpostorRoles = new HashSet<string>();

            for (int i = 0; i < settings.ImpostorsAmount; i++)
            {
                var assignedRole = nameof(Role.Impostor);

                foreach (var (role, chance) in specialImpostorRoles)
                {
                    if (!usedImpostorRoles.Contains(role) && random.Next(100) < chance)
                    {
                        assignedRole = role;
                        usedImpostorRoles.Add(role);
                        break;
                    }
                }

                players[i].Role = assignedRole;
            }

            // Specjalne role crewmate/neutralne — każda może wystąpić co najwyżej raz
            var specialCrewmateRoles = new List<(string Role, int Chance)>
            {
                (nameof(Role.Detective), settings.DetectiveChance),
                (nameof(Role.Doctor),    settings.DoctorChance),
                (nameof(Role.Mayor),     settings.MayorChance),
                (nameof(Role.Sheriff),   settings.SheriffChance),
                (nameof(Role.Jester),    settings.JesterChance),
                (nameof(Role.Renegate),  settings.RenegateChance),
            };

            int crewmateIndex = settings.ImpostorsAmount;

            foreach (var (role, chance) in specialCrewmateRoles)
            {
                if (crewmateIndex >= players.Count)
                    break;

                if (random.Next(100) < chance)
                {
                    players[crewmateIndex].Role = role;
                    crewmateIndex++;
                }
            }
        }
        #endregion

        #region CheckGameAfterKill()
        public async Task CheckGameAfterKill()
        {
            var playersClient = tableService.GetTableClient(DbPlayer.TableName);
            var players = new List<DbPlayer>();
            await foreach (var player in playersClient.QueryAsync<DbPlayer>())
                players.Add(player);

            var aliveCrewmates = players.Count(p => p.IsAlive && p.IsCrewmate());
            var aliveImpostors = players.Count(p => p.IsAlive && p.IsImpostor());
            var aliveFactionPlayers = aliveCrewmates + aliveImpostors;

            var settingsClient = tableService.GetTableClient(DbSettings.TableName);
            DbSettings settings;
            try
            {
                settings = (await settingsClient.GetEntityAsync<DbSettings>("Settings", "main")).Value;
            }
            catch (RequestFailedException) { return; }

            if (aliveFactionPlayers == 1 && players.Any(p => p.IsAlive && p.Role == nameof(Role.Renegate)))
            {
                settings.EndGame(Role.Renegate);
                await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

                return;
            }

            if (aliveImpostors <= 0)
            {
                settings.EndGame(Role.Crewmate);
                await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

                return;
            }
            
            if (aliveImpostors >= aliveCrewmates && !players.Any(p => p.IsAlive && p.Role == nameof(Role.Renegate)))
            {
                settings.EndGame(Role.Impostor);
                await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

                return;
            }
        }
        #endregion

        #region CheckGameAfterTask()
        public async Task CheckGameAfterTask()
        {
            var settingsClient = tableService.GetTableClient(DbSettings.TableName);
            DbSettings settings;
            try
            {
                settings = (await settingsClient.GetEntityAsync<DbSettings>("Settings", "main")).Value;
            }
            catch (RequestFailedException) { return; }

            var playersClient = tableService.GetTableClient(DbPlayer.TableName);
            var players = new List<DbPlayer>();
            await foreach (var player in playersClient.QueryAsync<DbPlayer>())
                players.Add(player);

            var crewmatesCount = players.Count(p => p.IsCrewmate());

            if (settings.CompletedTasksCount >= settings.TasksPerPlayer * crewmatesCount)
            {
                settings.EndGame(Role.Crewmate);
                await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);
            }
        }
        #endregion

        #region CheckGameAfterVoting()
        public async Task<bool> CheckGameAfterVoting()
        {
            var settingsClient = tableService.GetTableClient(DbSettings.TableName);
            DbSettings settings;
            try
            {
                settings = (await settingsClient.GetEntityAsync<DbSettings>("Settings", "main")).Value;
            }
            catch (RequestFailedException) { return false; }

            var playersClient = tableService.GetTableClient(DbPlayer.TableName);
            var votes = new List<string?>();
            await foreach (var player in playersClient.QueryAsync<DbPlayer>())
            {
                var playerVote = string.IsNullOrEmpty(player.VotedPerson) ? null : player.VotedPerson;

                if (player.Role == nameof(Role.Mayor) && settings.IsMayorUsed && !settings.MayorVoted)
                {
                    votes.Add(playerVote);
                    votes.Add(playerVote);

                    settings.MayorVoted = true;
                    await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);
                }

                votes.Add(playerVote);
            }

            var votedOut = votes
                .GroupBy(x => x)
                .Where(g => g.Count() == votes.GroupBy(x => x).Max(x => x.Count()))
                .Select(g => g.Key)
                .ToList();

            // Remis lub skip
            if (votedOut.Count != 1 || votedOut.First() is null)
                return false;

            var votedPlayer = await tableService.GetPlayer(votedOut.First());
            if (votedPlayer is null)
                throw new Exception("Not found voted player");

            votedPlayer.IsAlive = false;

            var tableClient = tableService.GetTableClient(DbPlayer.TableName);
            await tableClient.UpdateEntityAsync(votedPlayer, ETag.All, TableUpdateMode.Replace);

            if (votedPlayer.Role == nameof(Role.Jester))
                await JesterWins();
            else
                await CheckGameAfterKill();

            settings.IsVoting = false;
            await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);

            return true;
        }
        #endregion

        #region JesterWins()
        public async Task JesterWins()
        {
            var settingsClient = tableService.GetTableClient(DbSettings.TableName);
            DbSettings settings;
            try
            {
                settings = (await settingsClient.GetEntityAsync<DbSettings>("Settings", "main")).Value;
            }
            catch (RequestFailedException) { return; }


            settings.EndGame(Role.Jester);
            await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);
        }
        #endregion

        public static void Shuffle<T>(List<T> list)
        {
            for (int n = list.Count - 1; n > 0; n--)
            {
                int k = RandomNumberGenerator.GetInt32(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }
    }
}
