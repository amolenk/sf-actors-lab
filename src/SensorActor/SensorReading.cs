using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SensorActor
{
    [DataContract]
    public class SensorReading
    {
        [DataMember]
        public decimal Value { get; set; }

        [DataMember]
        public DateTime Timestamp { get; set; }
    }
}
