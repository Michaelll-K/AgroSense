namespace AgroSense.Models
{
    public class WeatherResponse
    {
        public Current current { get; set; }

        public class Current
        {
            public decimal temperature_2m { get; set; }
            public decimal wind_speed_10m { get; set; }
        }
    }
}
