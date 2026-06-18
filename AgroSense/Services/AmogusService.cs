using Azure;
using Azure.Data.Tables;
using AgroSense.Entities;
using AgroSense.Enums;
using AgroSense.Models.Admin;

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

            var aliveCrewmates = players.Count(p => p.IsAlive && !p.Role.Contains(Role.Impostor.ToString()));
            var aliveImpostors = players.Count(p => p.IsAlive && p.Role.Contains(Role.Impostor.ToString()));

            var settingsClient = tableService.GetTableClient(DbSettings.TableName);
            DbSettings settings;
            try
            {
                settings = (await settingsClient.GetEntityAsync<DbSettings>("Settings", "main")).Value;
            }
            catch (RequestFailedException) { return; }

            // TODO: dodanie logiki renegata

            if (aliveImpostors <= 0)
            {
                settings.IsGameActive = false;
                settings.WinnigTeam = Role.Crewmate.ToString();
                await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);
            }
            else if (aliveImpostors >= aliveCrewmates)
            {
                settings.IsGameActive = false;
                settings.WinnigTeam = Role.Impostor.ToString();
                await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);
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

            var crewmatesCount = players.Count(p => !p.Role.Contains(Role.Impostor.ToString()));

            if (settings.CompletedTasksCount >= settings.TasksPerPlayer * crewmatesCount)
            {
                settings.IsGameActive = false;
                settings.WinnigTeam = Role.Crewmate.ToString();
                await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);
            }
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

            settings.IsGameActive = false;
            settings.WinnigTeam = Role.Jester.ToString();
            await settingsClient.UpdateEntityAsync(settings, ETag.All, TableUpdateMode.Replace);
        }
        #endregion

        static void Shuffle<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                Random rng = new Random();
                n--;
                int k = rng.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }
    }
}
