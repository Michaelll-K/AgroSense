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
            var rolesGranted = 0;

            foreach (var player in players)
                player.Role = nameof(Role.Crewmate);

            Shuffle(players);

            for (int i = 0; i < settings.ImpostorsAmount; i++)
            {
                if (i == 0)
                    players[i].Role = nameof(Role.ImpostorBlackmailer);
                else
                    players[i].Role = nameof(Role.Impostor);
            }

            rolesGranted += settings.ImpostorsAmount;

            for (int i = 0; i < settings.DetectivesAmount; i++)
                players[i + rolesGranted].Role = nameof(Role.Detective);

            rolesGranted += settings.DetectivesAmount;

            for (int i = 0; i < settings.DoctorsAmount; i++)
                players[i + rolesGranted].Role = nameof(Role.Doctor);
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
