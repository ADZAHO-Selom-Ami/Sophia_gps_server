using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Web;
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
            AddCorsHeaders();

            var binding = new BasicHttpBinding
            {
                MaxReceivedMessageSize = 10 * 1024 * 1024, // 10 Mo
                ReaderQuotas = {
                        MaxStringContentLength = 10 * 1024 * 1024,
                        MaxArrayLength = 10 * 1024 * 1024
                    }
            };

            var _proxy = new ProxyClient(binding, new EndpointAddress("http://localhost:8090/MyService/Proxy/"));
            var itinerary = await GetAdequateItineraryAsync(origin, destination);
            Console.WriteLine("La récupération de l'itinéraire adéquant entre marcher et vélo est faite....");
            Console.WriteLine("salut");
            var itineraryJson = JsonConvert.SerializeObject(itinerary);
            await SendToQueue(itineraryJson);

            return itinerary;
        }

        private async Task<FullItineraryResult> GetAdequateItineraryAsync(string origin, string destination)
        {
            double totalWalkingTime = 0;
            double totalMultiModalTime = 0;
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
                Console.WriteLine("Retrieving coordinates...");
                var originCoordinates = await _proxy.GetCoordinatesAsync(origin).ConfigureAwait(false);
                var destinationCoordinates = await _proxy.GetCoordinatesAsync(destination).ConfigureAwait(false);
                Console.WriteLine("Coordinates retrieved successfully.");

                // Get walking itinerary
                var walkingItinerary = await _proxy.GetItineraryAsync(
                    new Position { Lat = originCoordinates.Lat, Lng = originCoordinates.Lon },
                    new Position { Lat = destinationCoordinates.Lat, Lng = destinationCoordinates.Lon },
                    TravelMode.Walking,
                    "walkingFull"

                ).ConfigureAwait(false);
                Console.WriteLine("Walking itinerary retrieved.");

                // Retrieve contracts
                var contractOrigin = await _proxy.GetContractByCityAsync(originCoordinates.Address.City).ConfigureAwait(false);
                var contractDestination = await _proxy.GetContractByCityAsync(destinationCoordinates.Address.City).ConfigureAwait(false);

                // Handle null contracts using the new helper method
                if (contractOrigin == null)
                {
                    Console.WriteLine("Origin city has no contract. Searching for the nearest city with a contract and a station...");
                    contractOrigin = await FindNearestCityWithContractAndStationAsync(new Position
                    {
                        Lat = originCoordinates.Lat,
                        Lng = originCoordinates.Lon
                    }, _proxy).ConfigureAwait(false);

                    if (contractOrigin == null)
                    {
                        Console.WriteLine("No contract found for the origin. Returning walking itinerary.");
                        return new FullItineraryResult
                        {
                            Itineraries = new List<Itinerary> { walkingItinerary },
                            Origin = new Position { Lat = originCoordinates.Lat, Lng = originCoordinates.Lon },
                            Destination = new Position { Lat = destinationCoordinates.Lat, Lng = destinationCoordinates.Lon }
                        };
                    }
                }

                if (contractDestination == null)
                {
                    Console.WriteLine("Destination city has no contract. Searching for the nearest city with a contract and a station...");
                    contractDestination = await FindNearestCityWithContractAndStationAsync(new Position
                    {
                        Lat = destinationCoordinates.Lat,
                        Lng = destinationCoordinates.Lon
                    }, _proxy).ConfigureAwait(false);

                    if (contractDestination == null)
                    {
                        Console.WriteLine("No contract found for the destination. Returning walking itinerary.");
                        return new FullItineraryResult
                        {
                            Itineraries = new List<Itinerary> { walkingItinerary },
                            Origin = new Position { Lat = originCoordinates.Lat, Lng = originCoordinates.Lon },
                            Destination = new Position { Lat = destinationCoordinates.Lat, Lng = destinationCoordinates.Lon }
                        };
                    }
                }

                // Retrieve nearest stations
                var firstStation = await _proxy.FindClosestStationAsync(new Position { Lat = originCoordinates.Lat, Lng = originCoordinates.Lon }, contractOrigin.Name).ConfigureAwait(false);
                var lastStation = await _proxy.FindClosestStationAsync(new Position { Lat = destinationCoordinates.Lat, Lng = destinationCoordinates.Lon }, contractDestination.Name).ConfigureAwait(false);

                if (firstStation == null || lastStation == null)
                {
                    Console.WriteLine("No nearby stations found; returning walking itinerary.");
                    return new FullItineraryResult
                    {
                        Itineraries = new List<Itinerary> { walkingItinerary },
                        Origin = new Position { Lat = originCoordinates.Lat, Lng = originCoordinates.Lon },
                        Destination = new Position { Lat = destinationCoordinates.Lat, Lng = destinationCoordinates.Lon }
                    };
                }

                // Create multi-modal itinerary
                var multiModalItinerary = new List<Itinerary>
            {
                await _proxy.GetItineraryAsync(new Position { Lat = originCoordinates.Lat, Lng = originCoordinates.Lon }, firstStation.Position, TravelMode.Walking, "walkingA").ConfigureAwait(false),
                await _proxy.GetItineraryAsync(firstStation.Position, lastStation.Position, TravelMode.Biking, "biking").ConfigureAwait(false),
                await _proxy.GetItineraryAsync(lastStation.Position, new Position { Lat = destinationCoordinates.Lat, Lng = destinationCoordinates.Lon }, TravelMode.Walking, "walkingB").ConfigureAwait(false)
            };

                // Calculate total times
                totalWalkingTime = walkingItinerary.Duration.TotalMinutes;
                totalMultiModalTime = multiModalItinerary.Sum(itinerary => itinerary.Duration.TotalMinutes);

                // Compare times and return optimal itinerary
                if (totalWalkingTime <= totalMultiModalTime)
                {
                    Console.WriteLine("Walking is faster; returning walking itinerary.");
                    return new FullItineraryResult
                    {
                        Itineraries = new List<Itinerary> { walkingItinerary },
                        Origin = new Position { Lat = originCoordinates.Lat, Lng = originCoordinates.Lon },
                        Destination = new Position { Lat = destinationCoordinates.Lat, Lng = destinationCoordinates.Lon }
                    };
                }

                Console.WriteLine("Multi-modal itinerary is faster; returning it.");
                return new FullItineraryResult
                {
                    Itineraries = multiModalItinerary,
                    FirstStation = firstStation,
                    LastStation = lastStation,
                    Origin = new Position { Lat = originCoordinates.Lat, Lng = originCoordinates.Lon },
                    Destination = new Position { Lat = destinationCoordinates.Lat, Lng = destinationCoordinates.Lon }
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to compute the full itinerary. Details: {ex.Message}", ex);
            }
        }

        private async Task<Contract> FindNearestCityWithContractAndStationAsync(Position position, ProxyClient proxy)
        {
            var nearestCityWithContract = await proxy.GetClosestCityWithContractAsync(position).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(nearestCityWithContract))
            {
                var contract = await proxy.GetContractByCityAsync(nearestCityWithContract).ConfigureAwait(false);
                if (contract != null)
                {
                    var station = await proxy.FindClosestStationAsync(position, contract.Name).ConfigureAwait(false);
                    if (station != null)
                        return contract;
                }
            }
            return null;
        }






        private async Task SendToQueue(string message)
        {
            Uri connecturi = new Uri("activemq:tcp://localhost:61616");
            IConnectionFactory factory = new ConnectionFactory(connecturi);
            using (IConnection connection = factory.CreateConnection())
            {
                connection.Start();
                using (ISession session = connection.CreateSession(AcknowledgementMode.AutoAcknowledge))
                {
                    IDestination destination = session.GetQueue("itineraryQueue");

                    await ClearQueue(session, destination);

                    using (IMessageProducer producer = session.CreateProducer(destination))
                    {
                        ITextMessage textMessage = producer.CreateTextMessage(message);
                        textMessage.NMSDeliveryMode = MsgDeliveryMode.Persistent;

                        Console.WriteLine("Sending message to the queue: " + textMessage.Text);
                        await Task.Run(() => producer.Send(textMessage));
                    }
                }
            }
        }

        private async Task ClearQueue(ISession session, IDestination destination)
        {
            using (IMessageConsumer consumer = session.CreateConsumer(destination))
            {
                IMessage message;
                while ((message = await Task.Run(() => consumer.Receive(TimeSpan.FromMilliseconds(100)))) != null)
                {
                    // Consommer tous les messages pour vider la file d'attente
                    Console.WriteLine("Clearing queue...");
                }
            }
        }


        public FullItineraryResult ReceiveFromQueue()
        {
            AddCorsHeaders();

            Uri connecturi = new Uri("activemq:tcp://localhost:61616");
            IConnectionFactory factory = new ConnectionFactory(connecturi);
            using (IConnection connection = factory.CreateConnection())
            {
                connection.Start();
                using (ISession session = connection.CreateSession(AcknowledgementMode.ClientAcknowledge))
                {
                    IDestination destination = session.GetQueue("itineraryQueue");
                    using (IMessageConsumer consumer = session.CreateConsumer(destination))
                    {
                        // Consomme immédiatement le dernier message disponible
                        IMessage message = consumer.Receive(TimeSpan.FromSeconds(1));

                        if (message != null && message is ITextMessage textMessage)
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
                        else
                        {
                            Console.WriteLine("No messages in the queue.");
                        }
                    }
                }
            }
            return null;
        }


        private void AddCorsHeaders()
        {
            var context = WebOperationContext.Current;
            if (context != null)
            {
                context.OutgoingResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                context.OutgoingResponse.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                context.OutgoingResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept");
            }
        }
    }

    public class FullItineraryResult
    {
        public List<Itinerary> Itineraries { get; set; }
        public Station FirstStation { get; set; }
        public Station LastStation { get; set; }
        public Position Origin { get; set; }
        public Position Destination { get; set; }
    }
}
