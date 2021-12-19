using System;
using System.Collections.Generic;
using System.Text;

namespace CompanionBot
{
    public enum PoiType { pokestop, gym};
    public enum EditType { type, name, latitude, longitude, ex_eligible};
    public class PortalData
    {
        public PoiType type;
        public string guid;
        public string name;
        public string lat;
        public string lng;
        public bool? isEx;
    }

    public class EditData
    {
        public PoiType oldType;
        public string oldName;
        public string guid;
        public string lat;
        public string lng;
        public Dictionary<EditType, string> edits;
    }

    public struct AddressResponse
    {
        public string display_name;
        public string error;
        // additional properties don't matter right now, i don't need them.
    }
}
