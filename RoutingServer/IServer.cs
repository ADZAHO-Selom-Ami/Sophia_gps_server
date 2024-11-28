using System.ServiceModel;
using System.ServiceModel.Web;
using System.Threading.Tasks;

namespace RoutingServer
{
    // REMARQUE : vous pouvez utiliser la commande Renommer du menu Refactoriser pour changer le nom d'interface "IService1" à la fois dans le code et le fichier de configuration.
    [ServiceContract]
    public interface IServer
    {
        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Wrapped, UriTemplate = "getItinerary?origin={origin}&destination={destination}")]
        Task<string> GetItinerary(string origin, string destination);

        [OperationContract]
        [WebInvoke(Method = "GET", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Wrapped, UriTemplate = "receiveItinerary")]
        string ReceiveFromQueue();
    }
}
