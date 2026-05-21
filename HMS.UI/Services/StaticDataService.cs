using System;
using System.Threading.Tasks;
using HMS.UI.Services;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;

namespace HMS.UI.Services
{
    public class StaticDataService
    {
        private readonly ApiClient _api;

        public StaticDataService(ApiClient api)
        {
            _api = api;
        }

        public async Task<MedicalData?> GetMedicalDataAsync()
        {
            try
            {
                var res = await _api.GetAsync<MedicalData>("/staticdata/medical");
                return res;
            }
            catch
            {
                return null;
            }
        }

        public async Task<CountryEntry[]?> GetCountriesAsync()
        {
            try
            {
                var res = await _api.GetAsync<CountryEntry[]>("/staticdata/countries");
                return res;
            }
            catch
            {
                return null;
            }
        }

        public record MedicalData(string[]? BloodGroups, string[]? Genotypes);
        public record CountryEntry(string Name, string[]? States);
    }
}
