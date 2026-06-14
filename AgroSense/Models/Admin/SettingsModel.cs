namespace AgroSense.Models.Admin
{
    public class SettingsModel
    {
        public int ShortTasksPerPlayer { get; set; }
        public int LongTasksPerPlayer { get; set; }
        public int ImpostorsAmount { get; set; }
        public int ImpostorBlackmailerChance { get; set; }
        public int ImpostorSniperChance { get; set; }
        public int DetectiveChance { get; set; }
        public int DoctorChance { get; set; }
        public int JesterChance { get; set; }
        public int RenegateChance { get; set; }
        public int PanicCooldownFromMinutes { get; set; }
        public int SabotageCooldownFromMinutes { get; set; }
        public int SabotageDeadlineFromMinutes { get; set; }
    }
}
