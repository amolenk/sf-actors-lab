using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace BuildingActor.Interfaces
{
    public interface IBuildingActorEvents : IActorEvents
    {
        void StatusReported(long buildingId, IDictionary<string, decimal> status);
    }
}
