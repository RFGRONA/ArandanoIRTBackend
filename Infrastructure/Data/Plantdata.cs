using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Plantdata
{
    public int Idplantstate { get; set; }

    public string Nameplant { get; set; } = null!;

    public DateTime Registeredat { get; set; }

    public DateTime? Updatedat { get; set; }

    public int? Statusid { get; set; }

    public int? Cropid { get; set; }

    public virtual Crop? Crop { get; set; }

    public virtual ICollection<Devicedata> Devicedata { get; set; } = new List<Devicedata>();

    public virtual ICollection<Plantstatehistory> Plantstatehistory { get; set; } = new List<Plantstatehistory>();

    public virtual ICollection<Sensordata> Sensordata { get; set; } = new List<Sensordata>();

    public virtual Status? Status { get; set; }

    public virtual ICollection<Thermaldata> Thermaldata { get; set; } = new List<Thermaldata>();
}
