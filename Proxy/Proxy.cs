using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace Proxy
{
    // REMARQUE : vous pouvez utiliser la commande Renommer du menu Refactoriser pour changer le nom de classe "Service1" à la fois dans le code et le fichier de configuration.
    public class Proxy : IProxy
    {
        //On utilisera l'api de JCDecaux pour récupérer les informations sur les stations et les contrats et l'api de openRouteService pour calculer les itinéraires

        // Calculer la distance entre deux positions à partir de GeoCoordinate
        public double CalculateDistance(Position origin, Position destination)
        {
            throw new NotImplementedException();
        }

        // Décider de l'itinéraire à partir de deux positions(si on doit marcher ou prendre un vélo)
        public string DecideItinerary(Position origin, Position destination)
        {
            throw new NotImplementedException();
        }

        // Trouver la station la plus proche de la position(origine ou destination)
        public Station FindClosestStation(Position position, List<Station> stations)
        {
            throw new NotImplementedException();
        }

        // Récupérer un contrat par ville(origine ou destination)
        public Contract GetContractByCity(string city)
        {
            throw new NotImplementedException();
        }

        // Récupérer la liste des contrats
        public List<Contract> GetContracts()
        {
            throw new NotImplementedException();
        }

        // Récupérer l'itinéraire à partir de deux positions
        public string GetItinerary(Position origin, Position destination)
        {
            throw new NotImplementedException();
        }

        // Récupérer l'itinéraire à partir de deux positions pour tout le parcours(marche + vélo + marche)
        public string GetInineraryForBike(string origin, string destination)
        {
            throw new NotImplementedException();
        }

        // Récupérer la liste des stations d'un contrat
        public List<Station> GetStations(string contractName)
        {
            throw new NotImplementedException();
        }
    }

    public class Contract
    {
        public string Name { get; set; }
    }

    public class Station
    {
        public string Name { get; set; }
        public Position Position { get; set; }
        public int AvailableBikes { get; set; }
        public int AvailableBikeStands { get; set; }
    }

    public class Position
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
