namespace AgroSense.Models.Admin
{
    public class TaskModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public bool IsCompleted { get; set; }
    }
}
