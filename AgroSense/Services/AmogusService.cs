using AgroSense.Entities;
using AgroSense.Enums;
using AgroSense.Models.Admin;
using MongoDB.Driver;
using System.Drawing;
using System.Linq;

namespace AgroSense.Services
{
    public class AmogusService
    {
        private static Random random = new Random();

        private readonly IMongoDatabase database;

        #region TasksService()
        public AmogusService(IMongoDatabase database)
        {
            this.database = database;
        }
        #endregion

        #region GetTasksForUser()
        public async Task<List<TaskModel>> GetTasksForPlayer(DbSettings settings, int playersCount)
        {
            var taksCollection = database.GetCollection<DbTask>(DbTask.DbName);

            var tasks = await taksCollection
                .Find(Builders<DbTask>.Filter.Empty)
                .ToListAsync();

            if (settings.TaskPerPlayer > tasks.Count)
                throw new ArgumentException("Żądana liczba elementów jest większa niż liczba dostępnych elementów.");

            return tasks
                .OrderBy(t => random.Next())
                .Take(settings.TaskPerPlayer)
                .Select(t => new TaskModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    Location = t.Location,
                    Description = t.Description,
                    IsCompleted = false
                })
                .ToList();
        }
        #endregion

        #region DetermineRoles()
        public void DetermineRoles(DbSettings settings, List<DbPlayer> players)
        {
            var rolesGranted = 0;

            foreach (var player in players)
            {
                player.Role = nameof(Role.Crewmate);
            }

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
            {
                players[i + rolesGranted].Role = nameof(Role.Detective);
            }

            rolesGranted += settings.DetectivesAmount;

            for (int i = 0; i < settings.DoctorsAmount; i++)
            {
                players[i + rolesGranted].Role = nameof(Role.Doctor);
            }
        }
        #endregion

        #region CheckGameAfterKill()
        public async Task CheckGameAfterKill()
        {
            var playersCollection = database.GetCollection<DbPlayer>(DbPlayer.DbName);

            var players = await playersCollection
                .Find(Builders<DbPlayer>.Filter.Empty)
                .ToListAsync();

            var aliveCrewamtes = players.Count(p => p.IsAlive && !p.Role.Contains(Role.Impostor.ToString()));

            var aliveImpostors = players.Count(p => p.IsAlive && p.Role.Contains(Role.Impostor.ToString()));

            var settingsCollection = database.GetCollection<DbSettings>(DbSettings.DbName);

            var settings = await settingsCollection
                .Find(Builders<DbSettings>.Filter.Empty)
                .FirstOrDefaultAsync();

            if (aliveImpostors <= 0)
            {
                settings.IsGameActive = false;
                settings.WinnigTeam = Role.Crewmate.ToString();
                
                await settingsCollection.ReplaceOneAsync(
                    Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                    settings
                );
            }
            else if (aliveImpostors >= aliveCrewamtes)
            {
                settings.IsGameActive = false;
                settings.WinnigTeam = Role.Impostor.ToString();

                await settingsCollection.ReplaceOneAsync(
                    Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                    settings
                );
            }
        }
        #endregion

        #region CheckGameAfterTask()
        public async Task CheckGameAfterTask()
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

            if (settings.CompletedTasksCount >= settings.TaskPerPlayer * crewmatesCount)
            {
                settings.IsGameActive = false;
                settings.WinnigTeam = Role.Crewmate.ToString();

                await settingsCollection.ReplaceOneAsync(
                    Builders<DbSettings>.Filter.Eq(s => s.Id, settings.Id),
                    settings
                );
            }
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
