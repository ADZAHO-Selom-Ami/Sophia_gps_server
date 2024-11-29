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
            //Create a URI to serve as the base address
            Uri httpUrl = new Uri("http://localhost:8080/MyService/RoutingService");

            //Create WebServiceHost for RESTful service
            WebServiceHost host = new WebServiceHost(typeof(Server), httpUrl);

            //Add a service endpoint
            host.AddServiceEndpoint(typeof(IServer), new WebHttpBinding(), "").Behaviors.Add(new WebHttpBehavior());

            //Enable metadata exchange
            ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
            smb.HttpGetEnabled = true;
            host.Description.Behaviors.Add(smb);

            //Start the Service
            host.Open();

            Console.WriteLine("Service is host at " + DateTime.Now.ToString());
            Console.WriteLine("The service is ready at {0}", httpUrl);
            Console.WriteLine("Host is running... Press <Enter> key to stop");
            Console.ReadLine();

            //Close the Service
            host.Close();
        }
    }
}
