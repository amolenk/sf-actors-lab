using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Actors.Client;
using SensorActor.Interfaces;
using BuildingActor.Interfaces;

namespace SensorActor
{
    /// <remarks>
    /// This class represents an actor.
    /// Every ActorID maps to an instance of this class.
    /// The StatePersistence attribute determines persistence and replication of actor state:
    ///  - Persisted: State is written to disk and replicated.
    ///  - Volatile: State is kept in memory only and replicated.
    ///  - None: State is kept in memory only and not replicated.
    /// </remarks>
    [StatePersistence(StatePersistence.Persisted)]
    internal class SensorActor : Actor, ISensorActor
    {
        private IBuildingActor _building;

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "Actor activated.");

            _building = ActorProxy.Create<IBuildingActor>(
                new ActorId(1), "ServiceFabricSensors", "BuildingActorService");

            // The StateManager is this actor's private state store.
            // Data stored in the StateManager will be replicated for high-availability for actors that use volatile or persisted state storage.
            // Any serializable object can be saved in the StateManager.
            // For more information, see http://aka.ms/servicefabricactorsstateserialization

            return StateManager.TryAddStateAsync("readings", new SensorActorState());
        }

        public async Task UpdateReadingAsync(decimal reading, DateTime timestamp)
        {
            var readings = await StateManager.GetStateAsync<SensorActorState>("readings");

            readings.AddReading(reading, timestamp);

            await StateManager.SetStateAsync("readings", readings);

            ActorEventSource.Current.ActorMessage(this, "Got reading: {0}", reading);

            var averageReading = readings.Readings.Average(x => x.Value);

            await _building.ReportSensorStatusAsync(Id.GetStringId(), averageReading);
        }
    }
}
