using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace Proxy
{
    public class Proxy : IProxy
    {
        private const string NominatimUrl = "https://nominatim.openstreetmap.org/search";
        private const string ApiUrl = "https://api.jcdecaux.com/vls/v1";
        private const string OpenRouteUrl = "https://api.openrouteservice.org/v2/directions";
        private const string OpenRouteApiKey = "5b3ce3597851110001cf624881823d08157349c7abcde66f42aad2e3";
        private const string ApiKey = "5df25c847314dd583d69b046850275dd13334d96";
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

        public string GetClosestCityWithContract(Position position)
        {
            return GetClosestStationWithContractAsync(position).GetAwaiter().GetResult();
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

        private async Task<string> GetClosestStationWithContractAsync(Position position)
        {

            Console.WriteLine("Récupération du contrat le plus proche.");
            if (position == null)
            {
                Console.WriteLine("Les coordonnées fournies sont nulles.");
                return null;
            }

            // Récupération des contrats disponibles
            var contracts = await GetContractsAsync().ConfigureAwait(false);
            if (contracts == null || !contracts.Any())
            {
                Console.WriteLine("Aucun contrat disponible.");
                return null;
            }

            string nearestContract = null;
            double shortestDistance = double.MaxValue;

            foreach (var contract in contracts)
            {
                if (contract.Cities == null || !contract.Cities.Any())
                {
                    continue; // Si le contrat n'a pas de villes associées, on le saute
                }

                foreach (var city in contract.Cities)
                {
                    Console.WriteLine(city);
                    try
                    {
                        // Récupération des coordonnées de la ville
                        var cityGeocode = await GetCoordinatesAsync(city).ConfigureAwait(false);
                        if (cityGeocode == null || cityGeocode.Lat == null || cityGeocode.Lon == null)
                        {
                            Console.WriteLine($"Impossible de récupérer les coordonnées pour la ville {city}");
                            continue;
                        }

                        // Calcul de la distance entre la position et la ville
                        var cityCoordinate = new GeoCoordinate((double)cityGeocode.Lat, (double)cityGeocode.Lon);
                        var distance = new GeoCoordinate(position.Lat, position.Lng).GetDistanceTo(cityCoordinate);

                        // Mise à jour du contrat le plus proche
                        if (distance < shortestDistance)
                        {
                            shortestDistance = distance;
                            nearestContract = contract.Name;
                            Console.WriteLine($"Le nouveau contrat {contract.Name}");// On stocke le nom du contrat
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur lors de la récupération des coordonnées pour la ville {city}. Détails : {ex.Message}");
                        continue;
                    }
                }
            }

            if (nearestContract == null)
            {
                Console.WriteLine("Aucun contrat proche n'a été trouvé.");
            }
            else
            {
                Console.WriteLine($"Le contrat le plus proche est : {nearestContract}, à une distance de {shortestDistance} mètres.");
            }
            return nearestContract;

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
                    Console.WriteLine("Aucun contrat n'a été trouvé");
                    return null; 
                }

                // Mise en cache
                contractCache.Get("contracts", 300);  // Exemple d'expiration après 5 minutes
                return response;
            }
            catch (Exception ex)
            {
                return null; 
            }
        }

        private async Task<Contract> GetContractByCityAsync(string city)
        {
            Console.WriteLine($"Looking for the contract of {city}");

            if (city == null)
            {
                Console.WriteLine("La ville est null");
                return null;
            }

            // Normaliser la ville entrée
            var normalizedCity = NormalizeString(city);

            var contracts = await GetContractsAsync().ConfigureAwait(false);
            Console.WriteLine("Nous essayons de recuperer les contrats");

            if (contracts == null || !contracts.Any())
            {
                Console.WriteLine("Le contrat est null");
                return null;
            }

            Console.WriteLine("Les contrats ne sont pas null");

            // Rechercher un contrat correspondant en utilisant la normalisation
            var contract = contracts.FirstOrDefault(c =>
                c.Cities != null &&
                c.Cities.Any(cityName =>
                    NormalizeString(cityName).Equals(normalizedCity) || // Comparaison exacte
                    NormalizeString(cityName).IndexOf(normalizedCity, StringComparison.OrdinalIgnoreCase) >= 0 || // Inclusion partielle
                    normalizedCity.IndexOf(NormalizeString(cityName), StringComparison.OrdinalIgnoreCase) >= 0 // Inclusion inverse
                )
            );

            if (contract == null)
            {
                Console.WriteLine("Le contrat est null lorsque je récupère le contrat par ville");
                return null;
            }

            return contract;
        }

        private string NormalizeString(string input)
        {
            if (input == null) return null;

            var normalizedString = input.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark) // Retirer les caractères d'accents
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().ToLowerInvariant(); // Convertir en minuscules
        }


        private async Task<List<Station>> GetStationsAsync(string contractName)
        {
            Console.WriteLine("Nous essayons de recuperer les stations");
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
                    Console.WriteLine("Aucune réponse de l'API");

                   return null;
                }

                // Mise en cache
                stationCache.Get(contractName, 300);  // Exemple d'expiration après 5 minutes
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Le contrat est null lorsque je recupere les stations");
                return null; 
            }
        }

        private async Task<Station> FindClosestStationInternalAsync(Position chosenPosition, string contractName)
        {
            Console.WriteLine($"Le nom du contrat en question {contractName}");
            Console.WriteLine("Récupération de la station la plus proche");
            var stations = await GetStationsAsync(contractName).ConfigureAwait(false);

            if (stations == null || !stations.Any())
            {
                Console.WriteLine("Les stations sont nul mon bg");
                return null; 
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
                ?? null;
        }


        private async Task<Itinerary> GetItineraryAsync(Position origin, Position destination, TravelMode mode, string key)
        {
            try
            {
                string modePath = mode == TravelMode.Walking ? "foot-walking" : "cycling-road";

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