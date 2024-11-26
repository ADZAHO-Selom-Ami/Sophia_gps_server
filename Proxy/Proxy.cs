using Nest;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Device.Location;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using GeoCoordinate = System.Device.Location.GeoCoordinate;

namespace Proxy
{
    public class Proxy : IProxy

    {
        private static readonly HttpClient client = new HttpClient();
        private const string ApiKey = "8d9168bd963b85bab2899622e5d944bf9fc7e53a";
        private const string ApiUrl = "https://api.jcdecaux.com/vls/v1";
        public double CalculateDistance(Position origin, Position destination)
        {
            var originCoordinate = new GeoCoordinate(origin.Lat, origin.Lng);
            var destinationCoordinate = new GeoCoordinate(destination.Lat, destination.Lng);
            return originCoordinate.GetDistanceTo(destinationCoordinate);
        }




        public Station FindClosestStation(Position chosenStation, List<Station> stations)
        {
            var chosenCoordinate = new System.Device.Location.GeoCoordinate(chosenStation.Lat, chosenStation.Lng);
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

        public async Task<string> GetInineraryByWalking(Position origin, Position destination)
        {
            try
            {
                var response = await client.GetFromJsonAsync<string>($"{ApiUrl}/routes?origin={origin.Lat},{origin.Lng}&destination={destination.Lat},{destination.Lng}&mode=walking&apiKey={ApiKey}");
                if (response == null)
                {
                    throw new Exception("Failed to retrieve walking itinerary");
                }
                return response;
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while retrieving the walking itinerary: " + ex.Message);
            }
        }

        public async Task<string> GetInineraryForBike(Position origin, Position destination)
        {
            try
            {
                var response = await client.GetFromJsonAsync<string>($"{ApiUrl}/routes?origin={origin.Lat},{origin.Lng}&destination={destination.Lat},{destination.Lng}&mode=biking&apiKey={ApiKey}");
                if (response == null)
                {
                    throw new Exception("Failed to retrieve biking itinerary");
                }
                return response;
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while retrieving the biking itinerary: " + ex.Message);
            }

        }

        public async Task<string> GetFullItineraryInCaseOfBike(Position origin, Position destination)
        {
            try
            {
                

                var stations = await GetStations(GetCityNameByCoordinates(origin.Lat , origin.Lng).ToString());
                var firstStation = FindClosestStation(origin, stations);

                var lastStation = FindClosestStation(destination, stations);

                var walkItinerary = await GetInineraryByWalking(origin, firstStation.Position);

                var bikeItinerary = await GetInineraryForBike(firstStation.Position, lastStation.Position);

                var finalItinerary = await GetInineraryForBike(lastStation.Position, destination);

                
                return $"Walk to bike station: {walkItinerary} \n" +
                       $"Bike to second station: {bikeItinerary} \n" +
                       $"Final leg to destination: {finalItinerary}";
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to retrieve full itinerary: " + ex.Message);
            }
        }



        public string GetInstructions(string intinerary)
        {
            throw new NotImplementedException();
        }


        public async Task<string> GetCityNameByCoordinates(double lat, double lng)
        {
            try
            {
                

                var response = await client.GetFromJsonAsync<ReverseGeocodeResponse>(ApiUrl);
                if (response == null || string.IsNullOrEmpty(response.City))
                {
                    throw new Exception("Failed to retrieve city name from coordinates");
                }
                return response.City;
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while retrieving the city name: " + ex.Message);
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

        public async Task<string> DecideItinerary(Position origin, Position destination)
        {
            try
            {
                var walkingEstimate = await GetWalkingTime(origin, destination);
                var bikingEstimate = await GetBikingTimeWithStations(origin, destination);

                if (walkingEstimate.TotalMinutes < bikingEstimate.TotalMinutes)
                {
                    return "Walking is faster for this route.";
                }
                else
                {
                    return "Biking is faster for this route.";
                }
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while deciding the itinerary: " + ex.Message);
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

        public async Task<TimeSpan> GetBikingTimeWithStations(Position origin, Position destination)
        {
            var stations = await GetStations(GetCityNameByCoordinates(origin.Lat, origin.Lng).ToString());
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

      
        public class TravelTimeResponse
        {
            public double EstimatedMinutes { get; set; }
        }



    }


   
    public class ReverseGeocodeResponse
    {
        public string City { get; set; }
    }
}