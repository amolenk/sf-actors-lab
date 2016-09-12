using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BuildingActor.Interfaces;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using SensorActor.Interfaces;

namespace ClientApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var _ = Task.Run(() => GenerateSensorDataAsync());

            SubscribeToBuildingEventsAsync(1).GetAwaiter().GetResult();

            Console.ReadKey();
        }

        private static async Task GenerateSensorDataAsync()
        {
            var random = new Random();

            var sensorActors = Enumerable.Range(1, 3)
                .Select(i => new ActorId("Sensor" + i))
                .Select(id => ActorProxy.Create<ISensorActor>(id, "ServiceFabricSensors", "SensorActorService"));

            while (true)
            {
                try
                {
                    foreach (var sensor in sensorActors)
                    {
                        var dummyTemp = (decimal)(random.Next(1600, 2300) / 100);

                        await sensor.UpdateReadingAsync(dummyTemp, DateTime.UtcNow);
                    }
                }
                catch (FabricServiceNotFoundException)
                {
                    // Cluster is not ready yet.
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        private static async Task SubscribeToBuildingEventsAsync(long buildingId)
        {
            var eventHandler = new BuildingActorEventHandler();

            var building = ActorProxy.Create<IBuildingActor>(
                new ActorId(buildingId), "ServiceFabricSensors", "BuildingActorService");

            var subscribed = false;

            while (!subscribed)
            {
                try
                {
                    await building.SubscribeAsync<IBuildingActorEvents>(eventHandler);
                    subscribed = true;
                }
                catch (FabricException)
                {
                    // Application not ready yet, wait a while.
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }
    }
}
