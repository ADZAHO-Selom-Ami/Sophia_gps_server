using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace RoutingServer
{
    public class Program
    {
        static void Main(string[] args)
        {
            Uri baseAddress = new Uri("http://localhost:8080/RoutingService");

            using (WebServiceHost host = new WebServiceHost(typeof(Server), baseAddress))
            {
                try
                {
                    ServiceEndpoint endpoint = host.AddServiceEndpoint(typeof(IServer), new WebHttpBinding(), "");
                    endpoint.EndpointBehaviors.Add(new WebHttpBehavior());

                    host.Open();

                    Console.WriteLine("The service is ready at {0}", baseAddress);
                    Console.WriteLine("Press <Enter> to stop the service.");
                    Console.ReadLine();

                    host.Close();
                }
                catch (CommunicationException ce)
                {
                    Console.WriteLine("An exception occurred: {0}", ce.Message);
                    host.Abort();
                }
            }
        }
    }

}
