using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Actors.Client;
using BuildingActor.Interfaces;

namespace BuildingActor
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
    internal class BuildingActor : Actor, IBuildingActor, IRemindable
    {
        private const string PublishStatusReminder = "PublishBuildingStatus";

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "Actor activated.");

            return RegisterReminderAsync(PublishStatusReminder, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public Task ReportSensorStatusAsync(string sensorId, decimal averageReading)
        {
            return StateManager.SetStateAsync(sensorId, averageReading);
        }

        public async Task ReceiveReminderAsync(string reminderName, byte[] context, TimeSpan dueTime, TimeSpan period)
        {
            if (reminderName == PublishStatusReminder)
            {
                var status = new Dictionary<string, decimal>();
                var sensors = await StateManager.GetStateNamesAsync();

                foreach (var sensorId in sensors)
                {
                    var reading = await StateManager.GetStateAsync<decimal>(sensorId);

                    status.Add(sensorId, reading);
                }

                var buildingEvents = GetEvent<IBuildingActorEvents>();
                buildingEvents.StatusReported(this.GetActorId().GetLongId(), status);
            }
        }
    }
}
