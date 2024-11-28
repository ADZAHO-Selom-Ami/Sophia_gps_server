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
using System.Runtime.Serialization;

namespace Proxy
{
    public class Proxy : IProxy
    {
        private const string NominatimUrl = "https://nominatim.openstreetmap.org/search";
        private const string ApiUrl = "https://api.jcdecaux.com/vls/v1";
        private const string OpenRouteUrl = "https://api.openrouteservice.org/v2/directions";
        private const string ApiKey = "8d9168bd963b85bab2899622e5d944bf9fc7e53a";
        private static readonly HttpClient client = new HttpClient();


        //Add method to check if same contract or not.

        //Add method to get the nearest station without comparing to each station

        //Peu de temps pour calculer la distance

        //Bonne architecture de code

        //CORS...


        public GeocodeResponse GetCoordinates(string city)
        {
            var response = client.GetFromJsonAsync<List<GeocodeResponse>>($"{NominatimUrl}?q={Uri.EscapeDataString(city)}&format=json&addressdetails=1&limit=1").Result;
            if (response != null && response.Any())
            {
                var location = response.First();

                var latitude = location.Lat;
                var longitude = location.Lon;
                var name = location.DisplayName;
                var address = location.Address;

                return new GeocodeResponse { Lat = latitude, Lon = longitude, DisplayName = name, Address = address };

            }
            throw new Exception("No results returned from geolocation API.");
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

        public async Task<Contract> GetContractByCityAsync(string city)
        {
            var contracts = await GetContracts();
            foreach (var contract in contracts)
            {
                if (contract.Cities.Contains(city))
                {
                    return contract;
                }

            }
            throw new Exception("Failed to find contract for city");

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

        public double CalculateDistance(Position origin, Position destination)
        {
            var originCoordinate = new GeoCoordinate(origin.Lat, origin.Lng);
            var destinationCoordinate = new GeoCoordinate(destination.Lat, destination.Lng);
            return originCoordinate.GetDistanceTo(destinationCoordinate);
        }

        public async Task<Itinerary> GetItinerary(Position origin, Position destination, TravelMode mode)
        {
            try
            {
                string modePath = mode == TravelMode.Walking ? "foot-walking" : "cycling-regular";
                var url = $"{OpenRouteUrl}/{modePath}?api_key={ApiKey}&start={origin.Lng},{origin.Lat}&end={destination.Lng},{destination.Lat}";

                var response = await client.GetFromJsonAsync<Itinerary>(url);
                if (response == null)
                {
                    throw new Exception("Failed to retrieve itinerary");
                }

                return response;

            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching {mode.ToString().ToLower()} itinerary: {ex.Message}");
            }
        }

        public async Task<List<Itinerary>> GetFullItineraryInCaseOfBike(string origin, string destination)
        {
            try
            {
                var originCoordinates = GetCoordinates(origin);
                var destinationCoordinates = GetCoordinates(destination);

                var contractOrigin = await GetContractByCityAsync(originCoordinates.Address.Town);
                var contractDestination = await GetContractByCityAsync(destinationCoordinates.Address.Town);

                var stationsOrigin = await GetStations(contractOrigin.Name);
                var stationsDestination = await GetStations(contractDestination.Name);

                Position originPosition = new Position { Lat = originCoordinates.Lat, Lng = originCoordinates.Lon };
                Position destinationPosition = new Position { Lat = destinationCoordinates.Lat, Lng = destinationCoordinates.Lon };
                var firstStation = FindClosestStation(originPosition, stationsOrigin);
                var lastStation = FindClosestStation(destinationPosition, stationsDestination);

                var itinerary = new List<Itinerary>();

                Itinerary walkingItineraryOrigin = await GetItinerary(originPosition, firstStation.Position, TravelMode.Walking);
                Itinerary bikeItinerary = await GetItinerary(firstStation.Position, lastStation.Position, TravelMode.Biking);
                Itinerary walkingItineraryDestination = await GetItinerary(lastStation.Position, destinationPosition, TravelMode.Walking);

                itinerary.Add(walkingItineraryOrigin);
                itinerary.Add(bikeItinerary);
                itinerary.Add(walkingItineraryDestination);

                return itinerary;

            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve full itinerary: {ex.Message}");
            }
        }

    }
    public class TravelTimeResponse
    {
        public double EstimatedMinutes { get; set; }
    }

    public class GeocodeResponse
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }

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

    public class Itinerary
    {
        public List<Step> Steps { get; set; }
        public double Distance { get; set; }
        public TimeSpan Duration { get; set; }
        public List<Position> Coordinates { get; set; }
    }

    public class Step
    {
        public double Distance { get; set; }
        public double Duration { get; set; }
        public string Instruction { get; set; }
        public string Name { get; set; }
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


        public int AvailableStand { get; set; }


        public int AvailableBike { get; set; }
    }

    public class Contract
    {
        public string Name { get; set; }
        public List<string> Cities { get; set; }
    }

}
