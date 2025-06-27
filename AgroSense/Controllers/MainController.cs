using AgroSense.Entities;
using AgroSense.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace AgroSense.Controllers
{
    [ApiController]
    [Route("api/main")]
    public class MainController : ControllerBase
    {
        private readonly IMongoDatabase database;
        private readonly IConfiguration configuration;

        public MainController(IMongoDatabase database, IConfiguration configuration)
        {
            this.database = database;
            this.configuration = configuration;
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
                result.Advice = nameof(Advices.Watering);
            }
            else if (result.Humidity > 21)
            {
                result.Advice = nameof(Advices.None);
            }
            else
            {
                result.Advice = nameof(Advices.Drying);
            }

            return result;
        }

        [Authorize]
        [HttpGet("history")]
        public async Task<ActionResult<List<HumidityHistory>>> GetHistory()
        {
            var history = database.GetCollection<HumidityHistory>("humidityHistory");

            return await history.Find(Builders<HumidityHistory>.Filter.Empty).ToListAsync();
        }

        [Authorize]
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

        [Authorize]
        [HttpPost("start-watering")]
        public ActionResult<string> StartWathering()
        {
            Thread.Sleep(TimeSpan.FromSeconds(5));
        
            return $"Nawodnienie rozpoczęte";
        }

        [Authorize]
        [HttpPost("stop-watering")]
        public ActionResult<string> StopWathering()
        {
            Thread.Sleep(TimeSpan.FromSeconds(5));
        
            return $"Nawodnienie zakończone"; // test
        }

        [HttpPost("login")]
        public ActionResult<string> Login([FromBody] AuthModel model)
        {
            if (model.Username != configuration["UserAuth:Login"] || model.Password != configuration["UserAuth:Pass"])
            {
                return BadRequest("Błędne dane logowania");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, model.Username)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: configuration["Jwt:Issuer"],
                audience: configuration["Jwt:Issuer"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
