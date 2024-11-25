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
        public double CalculateDistance(Position origin, Position destination)
        {
            throw new NotImplementedException();
        }

        public string DecideItinerary(Position origin, Position destination)
        {
            throw new NotImplementedException();
        }

        public Station FindClosestStation(Position position, List<Station> stations)
        {
            throw new NotImplementedException();
        }

        public Contract GetContractByCity(string city)
        {
            throw new NotImplementedException();
        }

        public List<Contract> GetContracts()
        {
            throw new NotImplementedException();
        }

        public string GetIninerary(Position origin, Position destination)
        {
            throw new NotImplementedException();
        }

        public string GetInineraryForBike(Position origin, Position destination)
        {
            throw new NotImplementedException();
        }

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
    }

    public class Position
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
