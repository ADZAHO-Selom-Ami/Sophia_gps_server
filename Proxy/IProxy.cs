using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Proxy
{
    [ServiceContract]
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

        Task<string> GetFullItineraryInCaseOfBike(Position origin, Position destination, string city);


        [OperationContract]
        Task<List<Station>> GetStations(string contractName);



        [OperationContract]
        Task<TimeSpan> GetWalkingTime(Position origin, Position destination);

        [OperationContract]
        Task<TimeSpan> GetBikingTimeWithStations(Position origin, Position destination, string city);

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
