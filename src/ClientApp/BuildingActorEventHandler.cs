using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BuildingActor.Interfaces;

namespace ClientApp
{
    public class BuildingActorEventHandler : IBuildingActorEvents
    {
        public void StatusReported(long buildingId, IDictionary<string, decimal> status)
        {
            Console.WriteLine("Got status report for building {0}:", buildingId);
            
            foreach (var sensorStatus in status)
            {
                Console.WriteLine("{0} = {1} °C", sensorStatus.Key, sensorStatus.Value);
            }
        }
    }
}
