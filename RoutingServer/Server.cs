using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using ISession = Apache.NMS.ISession;

namespace RoutingServer
{
    public class Server : IServer
    {
        private readonly Proxy _proxy;

        public Server()
        {
            _proxy = new Proxy();
        }

        public async Task<string> GetItinerary(string origin, string destination)
        {
            // Appeler le serveur SOAP proxy pour obtenir l'itinéraire
            var itinerary = await _proxy.GetInineraryForBikeAsync(origin, destination);

            // Envoyer l'itinéraire dans une queue (par exemple, ActiveMQ)
            await SendToQueue(itinerary);

            return itinerary;
        }

        private async Task SendToQueue(string message)
        {
            Uri connecturi = new Uri("activemq:tcp://localhost:61616");
            IConnectionFactory factory = new ConnectionFactory(connecturi);
            using (IConnection connection = factory.CreateConnection())
            {
                connection.Start();
                using (ISession session = connection.CreateSession())
                {
                    IDestination destination = session.GetQueue("itineraryQueue");
                    using (IMessageProducer producer = session.CreateProducer(destination))
                    {
                        producer.DeliveryMode = MsgDeliveryMode.NonPersistent;
                        ITextMessage textMessage = producer.CreateTextMessage(message);
                        await Task.Run(() => producer.Send(textMessage));
                    }

                    session.Close();
                }
                connection.Close();
            }
        }

        public string ReceiveFromQueue()
        {
            Uri connecturi = new Uri("activemq:tcp://localhost:61616");
            IConnectionFactory factory = new ConnectionFactory(connecturi);
            using (IConnection connection = factory.CreateConnection())
            {
                connection.Start();
                using (ISession session = connection.CreateSession())
                {
                    IDestination destination = session.GetQueue("itineraryQueue");
                    using (IMessageConsumer consumer = session.CreateConsumer(destination))
                    {
                        ITextMessage message = consumer.Receive() as ITextMessage;
                        if (message != null)
                        {
                            return message.Text;
                        }
                    }

                    session.Close();
                }
                connection.Close();
            }
            return null;
        }
    }

    public class Position
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
