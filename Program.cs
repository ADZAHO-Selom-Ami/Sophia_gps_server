using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProxyDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            
            Proxy proxy = new Proxy();

            Position origin = new Position { Lat = 48.8566, Lng = 2.3522 }; // Coordinates for Paris
            Position destination = new Position { Lat = 48.858844, Lng = 2.294351 }; // Coordinates for Eiffel Tower in Paris

            // Calculate distance
            double distance = proxy.CalculateDistance(origin, destination);
            Console.WriteLine($"Distance between origin and destination: {distance} meters");

            // Decide the best route
            string bestRoute = await proxy.DecideItinerary(origin, destination);
            Console.WriteLine($"Best route: {bestRoute}");

            // Get city name by coordinates (assuming GetCityNameByCoordinates method is properly implemented and functional)
            try
            {
                string cityName = await proxy.GetCityNameByCoordinates(origin.Lat, origin.Lng);
                Console.WriteLine($"City Name: {cityName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            try
            {
                List<Station> stations = await proxy.GetStations("Paris");
                Station closestStation = proxy.FindClosestStation(origin, stations);
                Console.WriteLine($"Closest Station: {closestStation.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }