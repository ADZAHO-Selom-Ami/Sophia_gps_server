using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Proxy
{
    [ServiceContract] // Indicates that this interface defines a WCF service contract.
    public interface IProxy
    {

        [OperationContract]
        double CalculateDistance(Position origin, Position destination);

        [OperationContract]
        Station FindClosestStation(Position chosenStation, List<Station> stations);

        [OperationContract]
        Task<Contract> GetContractByCityAsync(string city);

        [OperationContract]
        Task<List<Contract>> GetContracts();

        [OperationContract]
        Task<string> GetInineraryByWalking(Position origin, Position destination);

        [OperationContract]
        Task<string> GetInineraryForBike(Position origin, Position destination);

        [OperationContract]
        Task<string> GetFullItineraryInCaseOfBike(Position origin, Position destination);

        [OperationContract]
        Task<string> GetCityNameByCoordinates(double lat, double lng);

        [OperationContract]
        Task<List<Station>> GetStations(string contractName);

        [OperationContract]
        Task<string> DecideItinerary(Position origin, Position destination);

        [OperationContract]
        Task<TimeSpan> GetWalkingTime(Position origin, Position destination);

        [OperationContract]
        Task<TimeSpan> GetBikingTimeWithStations(Position origin, Position destination);

        [OperationContract]
        Task<TimeSpan> GetBikingTime(Position origin, Position destination);
    }

    // Data contracts define how data is serialized and deserialized during service operations.
    [DataContract]
    public class Position
    {
        [DataMember]
        public double Lat { get; set; }
        [DataMember]
        public double Lng { get; set; }
    }

    [DataContract]
    public class Station
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public Position Position { get; set; }
    }

    [DataContract]
    public class Contract
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string City { get; set; }
    }
}
