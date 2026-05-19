using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace HMS.UI.Services
{
    public class StaticDataService
    {
        private readonly IWebHostEnvironment _env;

        public StaticDataService(IWebHostEnvironment env)
        {
            _env = env;
        }

        private string DataPath(string fileName) => Path.Combine(_env.WebRootPath ?? string.Empty, "data", fileName);

        public async Task<MedicalData?> GetMedicalDataAsync()
        {
            var path = DataPath("medical.json");
            if (!File.Exists(path)) return null;
            var txt = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<MedicalData>(txt, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task<CountryEntry[]?> GetCountriesAsync()
        {
            var path = DataPath("countries.json");
            if (!File.Exists(path)) return null;
            var txt = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<CountryEntry[]>(txt, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public record MedicalData(string[]? BloodGroups, string[]? Genotypes);
        public record CountryEntry(string Name, string[]? States);
    }
}