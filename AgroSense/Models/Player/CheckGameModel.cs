namespace AgroSense.Models.Player
{
    public class CheckGameModel
    {
        public List<PlayerInfo> PlayersInfo { get; set; }
        public bool IsGameActive { get; set; }
        public DateTime? SabotageStartDateUtc { get; set; }
        public string ImpostorsNames { get; set; }
        public bool IsPanic { get; set; }
        public string PanicReporter { get; set; }
        public bool IsCorpse { get; set; }
        public string CorpseReporter { get; set; }
        public DateTime? SabotageDeadlineDateUtc { get; set; }
        public int TasksToComplete { get; set; }
        public int CompletedTasks { get; set; }
        public string WinningTeam { get; set; }
        public bool IsBlackmailUsed { get; set; }
        public DateTime? SabotageCooldownDateUtc { get; set; }
        public DateTime? PanicCooldown { get; set; }
        public bool IsDetectiveUsed { get; set; }
    }

    public class PlayerInfo
    {
        public string Name { get; set; }
        public string Role { get; set; }
        public bool IsAlive { get; set; }
        public bool IsBlackmailed { get; set; }
    }
}
