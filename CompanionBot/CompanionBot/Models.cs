using System;
using System.Collections.Generic;
using System.Text;

namespace CompanionBot
{
    public class PortalData
    {
        public string type;
        public string guid;
        public string name;
        public string lat;
        public string lng;
        public bool? isEx;
    }

    public class EditData
    {
        public string oldType;
        public string oldName;
        public string guid;
        public string lat;
        public string lng;
        ///<remarks>expected Keys: type, name, latitude, longitude, ex_eligible</remarks>
        public Dictionary<string, string> edits;
    }

    public struct AddressResponse
    {
        public string display_name;
        public string error;
        // additional properties don't matter right now, i don't need them.
    }
}
