using AgroSense.Entities;
using AgroSense.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Text.Json;

namespace AgroSense.Controllers
{
    [ApiController]
    [Route("api/main")]
    public class MainController : ControllerBase
    {
        private readonly IMongoDatabase database;

        public MainController(IMongoDatabase database)
        {
            this.database = database;
        }

        [HttpGet("humidity")]
        public ActionResult<HumidityResponse> GetHumidity()
        {
            var result = new HumidityResponse
            {
                Humidity = new Random().Next(1, 26)
            };

            if (result.Humidity < 10)
            {
                result.Advice = Advices.Watering;
            }
            else if (result.Humidity > 21)
            {
                result.Advice = Advices.None;
            }
            else
            {
                result.Advice = Advices.Drying;
            }

            return result;
        }

        [HttpGet("history")]
        public async Task<ActionResult<List<HumidityHistory>>> GetHistory()
        {
            var history = database.GetCollection<HumidityHistory>("humidityHistory");

            return await history.Find(Builders<HumidityHistory>.Filter.Empty).ToListAsync();
        }

        [HttpPost("history")]
        public async Task<ActionResult> SaveHistory([FromQuery] int humidity)
        {
            var humidityHistory = new HumidityHistory
            {
                Humidity = humidity,
                DateUtc = DateTime.UtcNow
            };

            var history = database.GetCollection<HumidityHistory>("humidityHistory");

            await history.InsertOneAsync(humidityHistory);

            return Accepted();
        }

        [HttpGet("weather")]
        public async Task<ActionResult<string>> GetWeather()
        {
            var client = new HttpClient();

            var response = await client.GetAsync("https://api.open-meteo.com/v1/forecast?latitude=52.26&longitude=4.5569&hourly=temperature_2m&current=temperature_2m,wind_speed_10m");

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync();

            var weatherResponse = JsonSerializer.Deserialize<WeatherResponse>(result);

            return $"Obecna temperatura w Lisse wynosi {weatherResponse.current.temperature_2m}°C, a prędkość wiatru wynosi {weatherResponse.current.wind_speed_10m}m/s";
        }
    }
}
