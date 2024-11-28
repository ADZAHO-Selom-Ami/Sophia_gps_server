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
        GeocodeResponse GetCoordinates(string city);

        [OperationContract]
        double CalculateDistance(Position origin, Position destination);

        [OperationContract]
        Station FindClosestStation(Position chosenStation, List<Station> stations);

        [OperationContract]
        Task<Contract> GetContractByCityAsync(string city);

        [OperationContract]
        Task<List<Contract>> GetContracts();

        [OperationContract]
        Task<List<Station>> GetStations(string contractName);

        [OperationContract]
        Task<Itinerary> GetItinerary(Position origin, Position destination, TravelMode mode);

        [OperationContract]
        Task<List<Itinerary>> GetFullItineraryInCaseOfBike(string origin, string destination);



        /*        [OperationContract]
                Task<TimeSpan> GetWalkingTime(Position origin, Position destination);

                [OperationContract]
                Task<TimeSpan> GetBikingTimeWithStations(Position origin, Position destination, string city);

                [OperationContract]
                Task<TimeSpan> GetBikingTime(Position origin, Position destination);*/
    }

    // Data contracts define how data is serialized and deserialized during service operations.
}
