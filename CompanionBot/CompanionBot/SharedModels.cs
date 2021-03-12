using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


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
}

public class Progress
{
    public int Creations { get; set; }
    public int Edits { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
}

