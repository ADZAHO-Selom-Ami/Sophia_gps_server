using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Proxy
{
    public class Proxy : IProxy
    {
        private const string NominatimUrl = "https://nominatim.openstreetmap.org/search";
        private const string ApiUrl = "https://api.jcdecaux.com/vls/v1";
        private const string OpenRouteUrl = "https://api.openrouteservice.org/v2/directions";
        private const string OpenRouteApiKey = "5b3ce3597851110001cf624881823d08157349c7abcde66f42aad2e3";
        private const string ApiKey = "8d9168bd963b85bab2899622e5d944bf9fc7e53a";
        private static readonly HttpClient client = new HttpClient();

        // Cache instances
        private GenericProxyCache<List<Contract>> contractCache = new GenericProxyCache<List<Contract>>();
        private GenericProxyCache<List<Station>> stationCache = new GenericProxyCache<List<Station>>();

        // --------- MÉTHODES PUBLIQUES EXPOSÉES ---------

        public GeocodeResponse GetCoordinates(string city)
        {
            return GetCoordinatesAsync(city).GetAwaiter().GetResult();
        }

        public List<Contract> GetContracts()
        {
            return GetContractsAsync().GetAwaiter().GetResult();
        }

        public Contract GetContractByCity(string city)
        {
            return GetContractByCityAsync(city).GetAwaiter().GetResult();
        }

        public List<Station> GetStations(string contractName)
        {
            return GetStationsAsync(contractName).GetAwaiter().GetResult();
        }

        public Station FindClosestStation(Position chosenPosition, string contractName)
        {
            return FindClosestStationInternalAsync(chosenPosition, contractName).GetAwaiter().GetResult();
        }

        public Itinerary GetItinerary(Position origin, Position destination, TravelMode mode, string key)
        {
            return GetItineraryAsync(origin, destination, mode, key).GetAwaiter().GetResult();
        }

        // --------- MÉTHODES INTERNES ASYNCHRONES ---------

        private async Task<GeocodeResponse> GetCoordinatesAsync(string city)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{NominatimUrl}?q={Uri.EscapeDataString(city)}&format=json&addressdetails=1&limit=1");
                request.Headers.Add("User-Agent", "Sophia_gps/1.0 (adzahostacy@gmail.com)");

                var response = await client.SendAsync(request).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<List<GeocodeResponse>>().ConfigureAwait(false);
                    if (data == null || !data.Any())
                    {
                        throw new Exception($"No results returned from geolocation API for city: {city}");
                    }

                    return data.First();
                }
                else
                {
                    throw new Exception($"Failed to fetch coordinates. HTTP Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error while fetching coordinates for city: {city}. Details: {ex.Message}", ex);
            }
        }



        private async Task<List<Contract>> GetContractsAsync()
        {
            try
            {
                // Vérification du cache
                var contracts = contractCache.Get("contracts");
                if (contracts != null && contracts.Any())
                {
                    return contracts;
                }

                // Si non présent dans le cache, appel API
                var response = await client.GetFromJsonAsync<List<Contract>>($"{ApiUrl}/contracts?apiKey={ApiKey}").ConfigureAwait(false);
                if (response == null)
                {
                    throw new Exception("Failed to retrieve contracts.");
                }

                // Mise en cache
                contractCache.Get("contracts", 300);  // Exemple d'expiration après 5 minutes
                return response;
            }
            catch (Exception ex)
            {
                throw new Exception("Error while fetching contracts. Details: " + ex.Message, ex);
            }
        }

        private async Task<Contract> GetContractByCityAsync(string city)
        {
            var contracts = await GetContractsAsync().ConfigureAwait(false);

            if (contracts == null || !contracts.Any())
            {
                throw new Exception("No contracts available.");
            }

            var contract = contracts.FirstOrDefault(c =>
                c.Cities != null &&
                c.Cities.Any(cityName =>
                    cityName.Equals(city, StringComparison.OrdinalIgnoreCase) ||
                    cityName.IndexOf(city, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    city.IndexOf(cityName, StringComparison.OrdinalIgnoreCase) >= 0
                )
            );

            if (contract == null)
            {
                throw new Exception($"No contract found for city: {city}");
            }

            return contract;
        }


        private async Task<List<Station>> GetStationsAsync(string contractName)
        {
            try
            {
                // Vérification du cache
                var stations = stationCache.Get(contractName);
                if (stations != null && stations.Any())
                {
                    return stations;
                }

                // Si non présent dans le cache, appel API
                var response = await client.GetFromJsonAsync<List<Station>>($"{ApiUrl}/stations?contract={contractName}&apiKey={ApiKey}").ConfigureAwait(false);

                if (response == null)
                {
                    throw new Exception($"Failed to retrieve stations for contract: {contractName}");
                }

                // Mise en cache
                stationCache.Get(contractName, 300);  // Exemple d'expiration après 5 minutes
                return response;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error while fetching stations for contract: {contractName}. Details: {ex.Message}", ex);
            }
        }

        private async Task<Station> FindClosestStationInternalAsync(Position chosenPosition, string contractName)
        {
            var stations = await GetStationsAsync(contractName).ConfigureAwait(false);

            if (stations == null || !stations.Any())
            {
                throw new Exception("Station list is empty. Cannot find the closest station.");
            }

            var chosenCoordinate = new GeoCoordinate(chosenPosition.Lat, chosenPosition.Lng);
            return stations
                .Where(station => station.Status == "OPEN" && station.Available_bikes > 0)
                .Select(station => new
                {
                    Station = station,
                    Distance = chosenCoordinate.GetDistanceTo(new GeoCoordinate(station.Position.Lat, station.Position.Lng))
                })
                .OrderBy(x => x.Distance)
                .FirstOrDefault()?.Station
                ?? throw new Exception("No closest station found.");
        }


        private async Task<Itinerary> GetItineraryAsync(Position origin, Position destination, TravelMode mode, string key)
        {
            try
            {
                string modePath = mode == TravelMode.Walking ? "foot-walking" : "cycling-regular";

                var url = $"{OpenRouteUrl}/{modePath}?api_key={OpenRouteApiKey}&start={origin.Lng},{origin.Lat}&end={destination.Lng},{destination.Lat}";

                var response = await client.GetFromJsonAsync<OpenRouteResponse>(url).ConfigureAwait(false);

                if (response == null || response.Features == null || !response.Features.Any())
                {
                    throw new Exception($"Failed to retrieve {mode.ToString().ToLower()} itinerary.");
                }

                var shortestRoute = response.Features
                    .OrderBy(feature => feature.Properties.Summary.Distance)
                    .First();

                return new Itinerary
                {
                    Key = key,
                    Distance = shortestRoute.Properties.Summary.Distance,
                    Duration = TimeSpan.FromSeconds(shortestRoute.Properties.Summary.Duration),
                    Coordinates = shortestRoute.Geometry.Coordinates
                        .Select(coord => new Position { Lng = coord[0], Lat = coord[1] })
                        .ToList(),
                    Steps = shortestRoute.Properties.Segments
                        .SelectMany(segment => segment.Steps)
                        .Select(step => new Step
                        {
                            Distance = step.Distance,
                            Duration = step.Duration,
                            Instruction = step.Instruction,
                            Name = step.Name
                        }).ToList()
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error while fetching {mode.ToString().ToLower()} itinerary. Details: {ex.Message}", ex);
            }
        }
    }


    // --------- AUTRES CLASSES ---------

    public class GeocodeResponse
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
        public string Display_Name { get; set; }
        public Address Address { get; set; }
    }

    public class Address
    {
        public string Country { get; set; }
        public string City { get; set; }
    }

    public class Itinerary
    {
        public string Key { get; set; }
        public List<Step> Steps { get; set; }
        public double Distance { get; set; }
        public TimeSpan Duration { get; set; }
        public List<Position> Coordinates { get; set; }
    }

    public enum TravelMode
    {
        Walking,
        Biking
    }

    public class Position
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    public class Station
    {
        public int Number { get; set; }
        public string Name { get; set; }
        public Position Position { get; set; }
        public string Status { get; set; }
        public int Available_bike_stands { get; set; }
        public int Available_bikes { get; set; }
    }

    public class Contract
    {
        public string Name { get; set; }
        public List<string> Cities { get; set; }
    }

    public class OpenRouteResponse
    {
        public List<Feature> Features { get; set; }
    }

    public class Feature
    {
        public Geometry Geometry { get; set; }
        public Properties Properties { get; set; }
    }

    public class Geometry
    {
        public List<List<double>> Coordinates { get; set; }
    }

    public class Properties
    {
        public Summary Summary { get; set; }
        public List<Segment> Segments { get; set; }
    }

    public class Summary
    {
        public double Distance { get; set; }
        public double Duration { get; set; }
    }

    public class Segment
    {
        public List<Step> Steps { get; set; }
    }

    public class Step
    {
        public double Distance { get; set; }
        public double Duration { get; set; }
        public string Instruction { get; set; }
        public string Name { get; set; }
    }

}
