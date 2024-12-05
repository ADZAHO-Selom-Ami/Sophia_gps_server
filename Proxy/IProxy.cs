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
        Station FindClosestStation(Position chosenStation, string contractName);

        [OperationContract]
        Contract GetContractByCity(string city); // Pas de Task

        [OperationContract]
        List<Contract> GetContracts(); // Pas de Task

        [OperationContract]
        List<Station> GetStations(string contractName); // Pas de Task

        [OperationContract]
        Itinerary GetItinerary(Position origin, Position destination, TravelMode mode, string key);

        [OperationContract]
        string GetClosestCityWithContract(Position position);




    }


    // Data contracts define how data is serialized and deserialized during service operations.
}
