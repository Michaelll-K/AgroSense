namespace AgroSense.Models.Admin
{
    public class SettingsModel
    {
        public int ShortTasksPerPlayer { get; set; }
        public int LongTasksPerPlayer { get; set; }
        public int ImpostorsAmount { get; set; }
        public int DetectivesAmount { get; set; }
        public int DoctorsAmount { get; set; }
        public int PanicCooldownFromMinutes { get; set; }
        public int SabotageCooldownFromMinutes { get; set; }
        public int SabotageDeadlineFromMinutes { get; set; }
    }
}
