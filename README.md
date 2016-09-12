# Service Fabric Sensors

In this lab, we'll look at creating Service Fabric services using the Actor model.

## Creating the Sensor Actor

The first actor we'll create is a Sensor actor. This actor will represent a single temperature sensor in a building.
The actor receives data from the (faked) temperature sensor and will log the data in diagnostic events.

1. Open Visual Studio with **elevated privileges**.

2. Create a new Service Fabric Application solution called **ServiceFabricSensors** with a **SensorActor** Actor service.

3. The **SensorActor** contains some sample code. Remove the **GetCount** and **SetCount** methods, as well as the **TryAddStateAsync** line from the **OnActivateAsync** method.

4. Add a **SensorReading** class to the **SensorActor** project which is a container class for a single sensor reading.

    ```
    [DataContract]
    public class SensorReading
    {
        [DataMember]
        public decimal Value { get; set; }

        [DataMember]
        public DateTime Timestamp { get; set; }
    }
    ```

5. We'll use this **SensorReading** class in a new **SensorActorState** class which is used to store the complete actor state.
The **SensorActorState** class will keep track of the last 10 received readings.

    ```
    [DataContract]
    public class SensorActorState
    {
        private const int MaxReadingCount = 10;

        public SensorActorState()
        {
            Readings = new List<SensorReading>();
        }

        [DataMember]
        public List<SensorReading> Readings { get; set; }

        public void AddReading(decimal reading, DateTime timestamp)
        {
            Readings = Readings.Take(MaxReadingCount - 1).ToList();
            Readings.Insert(0, new SensorReading { Value = reading, Timestamp = timestamp });
        }
    }
    ```

6. In the **OnActivateAsync** of the **SensorActor** class, initialize the actor's state by adding a new **SensorActorState** instance to the state manager: 

    ```
    protected override Task OnActivateAsync()
    {
        ActorEventSource.Current.ActorMessage(this, "Actor activated.");

        // The StateManager is this actor's private state store.
        // Data stored in the StateManager will be replicated for high-availability for actors that use volatile or persisted state storage.
        // Any serializable object can be saved in the StateManager.
        // For more information, see http://aka.ms/servicefabricactorsstateserialization

        return StateManager.TryAddStateAsync("readings", new SensorActorState());
    }
    ```

7. In the same **SensorActor** class, add a new **UpdateReadingAsync** method that will accept a reading and add it to the actor's state:

    ```
    public async Task UpdateReadingAsync(decimal reading, DateTime timestamp)
    {
        var readings = await StateManager.GetStateAsync<SensorActorState>("readings");

        readings.AddReading(reading, timestamp);

        await StateManager.SetStateAsync("readings", readings);

        ActorEventSource.Current.ActorMessage(this, "Got reading: {0}", reading);
    }
    ```

8. Fix **ISensorActor** in the **SensorActor.Interfaces** project by removing the **GetCount** and **SetCount** members and adding the **UpdateReadingAsync** member:

    ```
    public interface ISensorActor : IActor
    {
        Task UpdateReadingAsync(decimal reading, DateTime timestamp);
    }
    ```

9. You should now be able to build the solution. Note that the **SensorActor** service is automatically added to the service manifest (ApplicationPackageRoot\ServiceManifest.xml in the **ServiceFabricSensors** project).

## Sending data to the Actor

10. Add a new **ClientApp** Console Application to the solution and change the platform target of the new project to **x64**.
We'll use this project to send faked sensor data to the actor instances.

11. Add a reference to the **Microsoft.ServiceFabric.Actors** NuGet package.

12. Add a reference to the **SensorActor.Interfaces** project. Note that we only need to reference the Interfaces project and not the actual implementation.

13. In **Program.cs** add a method to generate some dummy temperature sensor data:

    ```
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
    ```

14. Call the method from the **Main** entry point:

    ```
    static void Main(string[] args)
    {
        var _ = Task.Run(() => GenerateSensorDataAsync());

        Console.ReadKey();
    }
    ```

15. When we run the solution, we want both the **ClientApp** and the **ServiceFabricSensors** projects to start.
Right-click on the solution and choose **Properties**. Next, select *Multiple startup projects* and set the actions for both *ClientApp* and *ServiceFabricSensors* to **Start**.

16. Start the solution. Open the **Diagnostics Events** window (*View > Other Windows > Diagnostic Events*) to view the generated ETW logging.

## Vanaf hier moet de uitleg nog opgeschreven worden (code is al wel compleet)

### Building Actor toevoegen om gemiddelden aggregaties naartoe te sturen.

- Create a new project ServiceFabricSensors > Services > Add… > New Service Fabric Service…

- Implement ReportStatus

- Create reminder to write averages to log 

- IRemindable

- Write to log

- Run

### Event implementeren om gemiddelden naar UI te sturen

- IBuildingActorEvents

- ReceiveReminder aanpassen met call naar StatusReported

- SensorActor geeft data door aan BuildingActor
- Add reference to BuildingActor.Interfaces
- Create _building
- Update UpdateReadingAsync to forward data

- ClientApp: BuildingActorEventHandler class
- Program toevoegen: SubscribeToBuildingEventsAsync (en aanroepen)

- Run!

### TODO Dummy data pushen via Web Sockets (oftwel, hoe kun je custom communicatie protocollen gebruiken in SF)








