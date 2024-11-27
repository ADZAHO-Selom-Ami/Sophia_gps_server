u
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Device.Location;
using System.Net.Http;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Proxy
{
    public class Proxy : IProxy
    {
        private const string NominatimUrl = "https://nominatim.openstreetmap.org/search";
        private const string ApiUrl = "https://api.jcdecaux.com/vls/v1";
        private const string ApiKey = "8d9168bd963b85bab2899622e5d944bf9fc7e53a";
        private static readonly HttpClient client = new HttpClient();

     
        public double CalculateDistance(Position origin, Position destination)
        {
            var originCoordinate = new GeoCoordinate(origin.Lat, origin.Lng);
            var destinationCoordinate = new GeoCoordinate(destination.Lat, destination.Lng);
            return originCoordinate.GetDistanceTo(destinationCoordinate);
        }

        
        public Station FindClosestStation(Position chosenStation, List<Station> stations)
        {
            var chosenCoordinate = new GeoCoordinate(chosenStation.Lat, chosenStation.Lng);
            Station closestStation = null;
            double closestDistance = double.MaxValue;

            foreach (var station in stations)
            {
                var stationCoordinate = new GeoCoordinate(station.Position.Lat, station.Position.Lng);
                double distance = chosenCoordinate.GetDistanceTo(stationCoordinate);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestStation = station;
                }
            }

            if (closestStation == null)
            {
                throw new Exception("Failed to find the closest station");
            }

            return closestStation;
        }

        public async Task<Contract> GetContractByCityAsync(string city)
        {
            var contracts = await GetContracts();
            return contracts.FirstOrDefault(contract => contract.City.Equals(city, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<List<Contract>> GetContracts()
        {
            var response = await client.GetFromJsonAsync<List<Contract>>($"{ApiUrl}/contracts?apiKey={ApiKey}");
            if (response == null)
            {
                throw new Exception("Failed to retrieve contracts");
            }
            return response;
        }

        public enum TravelMode
        {
            Walking,
            Biking
        }

        
        public async Task<string> GetItinerary(Position origin, Position destination, TravelMode mode)
        {
            try
            {
                string modePath = mode == TravelMode.Walking ? "foot-walking" : "cycling-regular";
                var url = $"https://api.openrouteservice.org/v2/directions/{modePath}?api_key={ApiKey}&start={origin.Lng},{origin.Lat}&end={destination.Lng},{destination.Lat}";

                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching {mode.ToString().ToLower()} itinerary: {ex.Message}");
            }
        }

      
        public async Task<string> GetFullItineraryInCaseOfBike(Position origin, Position destination, string city)
        {
            try
            {
                var stations = await GetStations(city);
                var firstStation = FindClosestStation(origin, stations);
                var lastStation = FindClosestStation(destination, stations);

                var walkItinerary = await GetItinerary(origin, firstStation.Position, TravelMode.Walking);
                var bikeItinerary = await GetItinerary(firstStation.Position, lastStation.Position, TravelMode.Biking);
                var finalItinerary = await GetItinerary(lastStation.Position, destination, TravelMode.Walking);

                return $"Walk to bike station: {walkItinerary}\n" +
                       $"Bike to second station: {bikeItinerary}\n" +
                       $"Final leg to destination: {finalItinerary}";
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve full itinerary: {ex.Message}");
            }
        }

        public async Task<string> GetTownFromAddressAsync(string address)
        {
            string url = $"{NominatimUrl}?q={Uri.EscapeDataString(address)}&format=json&addressdetails=1&limit=1";

            try
            {
                var response = await client.GetStringAsync(url);
                var doc = JsonDocument.Parse(response);

                if (doc.RootElement.GetArrayLength() > 0 &&
                    doc.RootElement[0].TryGetProperty("address", out var addressElement) &&
                    addressElement.TryGetProperty("town", out var town))
                {
                    return town.GetString();
                }

                return "Town not found";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching town from address: {ex.Message}");
            }
        }

        public async Task<Position> GetCoordinates(string address)
        {
            try
            {
                var uri = $"{NominatimUrl}?q={Uri.EscapeDataString(address)}&format=json&addressdetails=1&limit=1";
                var response = await client.GetFromJsonAsync<List<GeocodeResponse>>(uri);

                if (response != null && response.Any())
                {
                    var location = response.First();

                    if (double.TryParse(location.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) &&
                        double.TryParse(location.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
                    {
                        return new Position { Lat = latitude, Lng = longitude };
                    }

                    throw new Exception($"Invalid coordinates returned: Lat={location.Lat}, Lon={location.Lon}");
                }

                throw new Exception("No results returned from geolocation API.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching geolocation for address '{address}': {ex.Message}");
            }
        }

        public async Task<List<Station>> GetStations(string contractName)
        {
            var response = await client.GetFromJsonAsync<List<Station>>($"{ApiUrl}/stations?contract={contractName}&apiKey={ApiKey}");
            if (response == null)
            {
                throw new Exception("Failed to retrieve stations");
            }
            return response;
        }

        public async Task<string> GetInstructions(string originAddress, string destinationAddress)
        {
            try
            {
                var origin = await GetCoordinates(originAddress);
                var city = GetTownFromAddressAsync(originAddress);
                var destination = await GetCoordinates(destinationAddress);
                var walkingEstimate = await GetWalkingTime(origin, destination);
                var bikingEstimate = await GetBikingTimeWithStations(origin, destination , city.ToString());

                if (walkingEstimate.TotalMinutes < bikingEstimate.TotalMinutes)
                {
                    return "Walking is recommended for this route.";
                }

                var fullBikeItinerary = await GetFullItineraryInCaseOfBike(origin, destination, "cityNameHere");
                return $"Biking is recommended for this route. Here's the detailed itinerary:\n{fullBikeItinerary}";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting instructions: {ex.Message}");
            }
        }

        public async Task<TimeSpan> GetWalkingTime(Position origin, Position destination)
        {
            var response = await client.GetFromJsonAsync<TravelTimeResponse>($"{ApiUrl}/timeestimate?origin={origin.Lat},{origin.Lng}&destination={destination.Lat},{destination.Lng}&mode=walking&apiKey={ApiKey}");
            if (response == null)
            {
                throw new Exception("Failed to retrieve walking time");
            }
            return TimeSpan.FromMinutes(response.EstimatedMinutes);
        }

        public async Task<TimeSpan> GetBikingTimeWithStations(Position origin, Position destination , string city)
        {
            //Trying to figure out how to get the city name here , from the adresses given for instructions
            var stations = await GetStations(city);
            var firstStation = FindClosestStation(origin, stations);
            var lastStation = FindClosestStation(destination, stations);

            var toFirstStationTime = await GetWalkingTime(origin, firstStation.Position);
            var bikingTime = await GetBikingTime(firstStation.Position, lastStation.Position);
            var fromLastStationTime = await GetWalkingTime(lastStation.Position, destination);

            return toFirstStationTime + bikingTime + fromLastStationTime;
        }

      

        public async Task<TimeSpan> GetBikingTime(Position origin, Position destination)
        {
            var response = await client.GetFromJsonAsync<TravelTimeResponse>($"{ApiUrl}/timeestimate?origin={origin.Lat},{origin.Lng}&destination={destination.Lat},{destination.Lng}&mode=biking&apiKey={ApiKey}");
            if (response == null)
            {
                throw new Exception("Failed to retrieve biking time");
            }
            return TimeSpan.FromMinutes(response.EstimatedMinutes);
        }
    }

    public class TravelTimeResponse
    {
        public double EstimatedMinutes { get; set; }
    }

    public class GeocodeResponse
    {
        [JsonPropertyName("lat")]
        public string Lat { get; set; }

        [JsonPropertyName("lon")]
        public string Lon { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("address")]
        public Address Address { get; set; }
    }

    public class Address
    {
        [JsonPropertyName("house_number")]
        public string HouseNumber { get; set; }

        [JsonPropertyName("road")]
        public string Road { get; set; }

        [JsonPropertyName("town")]
        public string Town { get; set; }
    }

   
}
