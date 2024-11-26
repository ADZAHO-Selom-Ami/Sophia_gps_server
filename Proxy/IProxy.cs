using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Proxy
{
    [ServiceContract]
    public interface IProxy
    {
        [OperationContract]
        List<Contract> GetContracts();

        [OperationContract]
        Contract GetContractByCity(string city);

        [OperationContract]
        List<Station> GetStations(string contractName);

        [OperationContract]
        Station FindClosestStation(Position position, List<Station> stations);

        [OperationContract]
        double CalculateDistance(Position origin, Position destination);

        [OperationContract]
        string DecideItinerary(Position origin, Position destination);

        [OperationContract]
        string GetItinerary(Position origin, Position destination);

        [OperationContract]
        string GetInineraryForBike(Position origin, Position destination);

    }

}
