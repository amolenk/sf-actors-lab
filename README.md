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

9. You should now be able to build the solution. Note that the **SensorActor** service is automatically added to the service manifest (*ApplicationPackageRoot\ServiceManifest.xml* in the **ServiceFabricSensors** project).

## Sending data to the Actor

1. Add a new **ClientApp** Console Application to the solution and change the platform target of the new project to **x64**.
We'll use this project to send faked sensor data to the actor instances.

2. Add a reference to the **Microsoft.ServiceFabric.Actors** NuGet package.

3. Add a reference to the **SensorActor.Interfaces** project. Note that we only need to reference the Interfaces project and not the actual implementation.

4. In **Program.cs** add a method to generate some dummy temperature sensor data:

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

5. Call the method from the **Main** entry point:

    ```
    static void Main(string[] args)
    {
        var _ = Task.Run(() => GenerateSensorDataAsync());

        Console.ReadKey();
    }
    ```

6. When we run the solution, we want both the **ClientApp** and the **ServiceFabricSensors** projects to start.
Right-click on the solution and choose **Properties**. Next, select *Multiple startup projects* and set the actions for both *ClientApp* and *ServiceFabricSensors* to **Start**.

7. Start the solution. Open the **Diagnostics Events** window (*View > Other Windows > Diagnostic Events*) to view the generated ETW logging.

## Adding a Building Actor to store aggregated readings

In this next part, we will add a new Building actor. The Sensor actors will forward aggregated readings to the Building actor.

1. Add a new **Actor Service** named **BuildingActor** to the solution (*ServiceFabricSensors > Services > Add… > New Service Fabric Service…*).

2. Remove the generated sample code from the **BuildActor** class and add a new method to receive sensor status data.
This method simply saves the average reading of each sensor in the state manager:

    ```
    public Task ReportSensorStatusAsync(string sensorId, decimal averageReading)
    {
        return StateManager.SetStateAsync(sensorId, averageReading);
    }
    ```
3. Update the **IBuildingActor** interface:

    ```
    public interface IBuildingActor : IActor
    {
        Task ReportSensorStatusAsync(string sensorId, decimal averageReading);
    }
    ```

4. The next step is to periodically write all average readings to the log. We will use Actor reminders for this. 
To use reminders, **BuildingActor** must first implement the **IRemindable** interface:

    ```
    internal class BuildingActor : Actor, IBuildingActor, IRemindable
    ```

5. In the implementation we should first check whether the fired reminder is the one we are waiting for. 
Then we collect all sensor readings from the state manager and write it to the log.

    ```
    private const string PublishStatusReminder = "PublishBuildingStatus";

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

            // Write status report to log.
            ActorEventSource.Current.ActorMessage(this, "Got status for {0} sensors.", status.Count);
            foreach (var sensorStatus in status)
            {
                ActorEventSource.Current.ActorMessage(this, "Status for sensor {0} = {1}.", sensorStatus.Key, sensorStatus.Value);
            }
        }
    }
    ```

6. Update the **OnActivateAsync** method to actually register the reminder:

    ```
    protected override Task OnActivateAsync()
    {
        ActorEventSource.Current.ActorMessage(this, "Actor activated.");

        return RegisterReminderAsync(PublishStatusReminder, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }
    ```

7. Now we need to update the **SensorActor** project to send aggregated data to a **BuildingActor**.
First add a reference to the **BuildingActor.Interfaces** project.

8. Add a private field to the **SensorActor** class to store a reference to the building actor.
We will assume there's a single building with id 1. In a more realistic scenario, the **SensorActor** could for example implement a **RegisterSensorAsync** method which could then be used to pass the correct building id to the sensor. 

    ```
    private IBuildingActor _building;

    protected override Task OnActivateAsync()
    {
        ActorEventSource.Current.ActorMessage(this, "Actor activated.");

        _building = ActorProxy.Create<IBuildingActor>(
            new ActorId(1), "ServiceFabricSensors", "BuildingActorService");

        return StateManager.TryAddStateAsync("readings", new SensorActorState());
    }
    ```

9. Add the following lines to the **UpdateReadingAsync** method to calculate an average of the last 10 sensor readings and report it to the building actor.
Note that we're using **Id.GetStringId()** to get the name of the sensor.

    ```
    var averageReading = readings.Readings.Average(x => x.Value);

    await _building.ReportSensorStatusAsync(Id.GetStringId(), averageReading);
    ```

10. Start the solution. Open the **Diagnostics Events** window (*View > Other Windows > Diagnostic Events*) to view the generated ETW logging.

## Using Events to update the UI

The Service Fabric Actor model provides a best effort eventing mechanism to send events to subscribers.
We will use this mechanism to update the user interface each time that the building actor is reporting the aggregated readings.

1. To publish events we must first declare the event in an interface. Add an **IBuildingActorEvents** interface to the **BuildingActor.Interfaces** project.
Note that the interface should derive from **IActorEvents**:

    ```
    public interface IBuildingActorEvents : IActorEvents
    {
        void StatusReported(long buildingId, IDictionary<string, decimal> status);
    }
    ```

2. Let **BuildingActor** implement **IActorEventPublisher<IBuildingActorEvents>**. This ensures that we can actually publish **IBuildingActorEvents** from the Building actor.
	
3. Change the **ReceiveReminderAsync** implementation to publish an event instead of writing the results to the log:

    ```
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

            // Publish event.
            var buildingEvents = GetEvent<IBuildingActorEvents>();
            buildingEvents.StatusReported(this.GetActorId().GetLongId(), status);
        }
    }
    ```

4. Now the **ClientApp** must subscribe to the events. First add a reference to the **BuildingActor.Interfaces** project so that we can use the **IBuildingActorEvents** interface from the **ClientApp**.

5. Add a new **BuildingActorEventHandler** class to the **ClientApp** which will be responsible for handling the events.
This class implements the **IBuildingActorEvents** interface:

    ```
    public void StatusReported(long buildingId, IDictionary<string, decimal> status)
    {
        Console.WriteLine("Got status report for building {0}:", buildingId);
        
        foreach (var sensorStatus in status)
        {
            Console.WriteLine("{0} = {1} °C", sensorStatus.Key, sensorStatus.Value);
        }
    }
    ```

6. In **Program.cs** add a new method to subscribe the handler to the building actor events.
Note that we catch **FabricExceptions** and retry because the Service Fabric application may not be started and ready yet when the **ClientApp** is started: 

    ```
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
    ```

7. Update the **Main** entry point to call the new method:

    ```
    static void Main(string[] args)
    {
        var _ = Task.Run(() => GenerateSensorDataAsync());

        SubscribeToBuildingEventsAsync(1).GetAwaiter().GetResult();

        Console.ReadKey();
    }
    ```

8. Start the solution. Every 5 seconds the aggregated readings will be outputted on the console.

### TODO Dummy data pushen via Web Sockets (oftwel, hoe kun je custom communicatie protocollen gebruiken in SF)
