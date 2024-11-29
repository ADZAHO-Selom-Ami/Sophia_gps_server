using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Newtonsoft.Json;
using RoutingServer.ProxyService;

namespace RoutingServer
{
    public class Server : IServer
    {
        public async Task<FullItineraryResult> GetItinerary(string origin, string destination)
        {
            var binding = new BasicHttpBinding
            {
                MaxReceivedMessageSize = 10 * 1024 * 1024, // 10 Mo
                ReaderQuotas = {
                    MaxStringContentLength = 10 * 1024 * 1024,
                    MaxArrayLength = 10 * 1024 * 1024
                }
            };

            var _proxy = new ProxyClient(binding, new EndpointAddress("http://localhost:8090/MyService/Proxy/"));
            var itinerary = await GetFullItineraryInCaseOfBikeAsync(origin, destination);

            var itineraryJson = JsonConvert.SerializeObject(itinerary);
            await SendToQueue(itineraryJson); 

            return itinerary;
        }

        private async Task<FullItineraryResult> GetFullItineraryInCaseOfBikeAsync(string origin, string destination)
        {
            var binding = new BasicHttpBinding
            {
                MaxReceivedMessageSize = 10 * 1024 * 1024,
                ReaderQuotas = {
                    MaxStringContentLength = 10 * 1024 * 1024,
                    MaxArrayLength = 10 * 1024 * 1024
                }
            };

            var _proxy = new ProxyClient(binding, new EndpointAddress("http://localhost:8090/MyService/Proxy/"));
            try
            {
                var originCoordinates = await _proxy.GetCoordinatesAsync(origin).ConfigureAwait(false);
                var destinationCoordinates = await _proxy.GetCoordinatesAsync(destination).ConfigureAwait(false);

                var contractOrigin = await _proxy.GetContractByCityAsync(originCoordinates.Address.City).ConfigureAwait(false);
                var contractDestination = await _proxy.GetContractByCityAsync(destinationCoordinates.Address.City).ConfigureAwait(false);

                var firstStation = await _proxy.FindClosestStationAsync(new Position { Lat = originCoordinates.Lat, Lng = originCoordinates.Lon }, contractOrigin.Name).ConfigureAwait(false);
                var lastStation = await _proxy.FindClosestStationAsync(new Position { Lat = destinationCoordinates.Lat, Lng = destinationCoordinates.Lon }, contractDestination.Name).ConfigureAwait(false);

                var itineraries = new List<Itinerary>
                {
                    await _proxy.GetItineraryAsync(new Position { Lat = originCoordinates.Lat, Lng = originCoordinates.Lon }, firstStation.Position, TravelMode.Walking, "walkingA").ConfigureAwait(false),
                    await _proxy.GetItineraryAsync(firstStation.Position, lastStation.Position, TravelMode.Biking, "biking").ConfigureAwait(false),
                    await _proxy.GetItineraryAsync(lastStation.Position, new Position { Lat = destinationCoordinates.Lat, Lng = destinationCoordinates.Lon }, TravelMode.Walking, "walkingB").ConfigureAwait(false)
                };

                return new FullItineraryResult
                {
                    Itineraries = itineraries,
                    FirstStation = firstStation,
                    LastStation = lastStation
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to compute the full itinerary with bikes. Details: {ex.Message}", ex);
            }
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
                }
            }
        }

        public FullItineraryResult ReceiveFromQueue()
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
                        IMessage message;
                        while ((message = consumer.Receive(TimeSpan.FromSeconds(1))) != null)
                        {
                            if (message is ITextMessage textMessage)
                            {
                                try
                                {
                                    var fullItinerary = JsonConvert.DeserializeObject<FullItineraryResult>(textMessage.Text);
                                    return fullItinerary;
                                }
                                catch (JsonSerializationException ex)
                                {
                                    Console.WriteLine($"Error deserializing message: {ex.Message}");
                                }
                            }
                        }
                        return null;
                    }
                }
            }
        }
    }

    public class FullItineraryResult
    {
        public List<Itinerary> Itineraries { get; set; }
        public Station FirstStation { get; set; }
        public Station LastStation { get; set; }
    }
}
