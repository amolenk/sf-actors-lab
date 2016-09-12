using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SensorActor
{
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
}
