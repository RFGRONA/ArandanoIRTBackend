using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Thermaldata
{
    public int Idthermaldata { get; set; }

    public string Thermalimagedata { get; set; } = null!;

    public byte[]? Rgbimagedata { get; set; }

    public DateTime Recordedat { get; set; }

    public int? Plantid { get; set; }

    public int? Cropid { get; set; }

    public virtual Crop? Crop { get; set; }

    public virtual Plantdata? Plant { get; set; }

    public virtual ICollection<Plantobservation> Plantobservation { get; set; } = new List<Plantobservation>();
}
