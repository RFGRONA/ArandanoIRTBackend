using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Plantobservation
{
    public int Idobservation { get; set; }

    public DateTime Createdat { get; set; }

    public string Description { get; set; } = null!;

    public bool Hasdecolorations { get; set; }

    public bool Hasaltereduniformity { get; set; }

    public string? Leafstemnotes { get; set; }

    public short Subjectiverating { get; set; }

    public string? Additionalnotes { get; set; }

    public int? Createdby { get; set; }

    public int? Statusid { get; set; }

    public int? Plantid { get; set; }

    public int? Lastsensordataid { get; set; }

    public int? Lastthermaldataid { get; set; }

    public virtual Person? CreatedbyNavigation { get; set; }

    public virtual Sensordata? Lastsensordata { get; set; }

    public virtual Thermaldata? Lastthermaldata { get; set; }

    public virtual Plantdata? Plant { get; set; }

    public virtual Status? Status { get; set; }
}
