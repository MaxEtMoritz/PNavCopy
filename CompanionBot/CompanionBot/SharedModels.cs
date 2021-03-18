using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CompanionBot
{
    public class PortalData
    {
        public string Type { get; set; }
        public string Guid { get; set; }
        public string Name { get; set; }
        public string Lat { get; set; }
        public string Lng { get; set; }
        public bool? IsEx { get; set; }
    }

    public class EditData
    {
        public string OldType { get; set; }
        public string OldName { get; set; }
        public string Guid { get; set; }
#nullable enable
        public string? Latitude { get; set; }
        public string? Longitude { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Ex_Eligible { get; set; }
#nullable restore
        public static explicit operator EditData(Bot.EditData data)
            => new EditData() {OldType = data.t.ToString(),
            OldName = data.n,
            Guid = "00000000000000000000000000000000.16",//TODO
            Latitude = data.e['a'],
            Longitude = data.e['o'],
            Name = data.e['n'],
            Type = data.e['t'] as Bot.LocationType.ToString(),
            
            }
    }

    public class Progress
    {
        public int Creations { get; set; }
        public int Edits { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public bool AttentionNeeded { get; set; }
    }
}
