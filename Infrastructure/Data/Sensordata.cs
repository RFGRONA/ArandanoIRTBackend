using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Sensordata
{
    public int Idsensordata { get; set; }

    public float Temperature { get; set; }

    public float Humidity { get; set; }

    public float Lightintensity { get; set; }

    public float? Citytemperature { get; set; }

    public float? Cityhumidity { get; set; }

    public DateTime Recordedat { get; set; }

    public int? Plantid { get; set; }

    public int? Cropid { get; set; }

    public virtual Crop? Crop { get; set; }

    public virtual Plantdata? Plant { get; set; }

    public virtual ICollection<Plantobservation> Plantobservation { get; set; } = new List<Plantobservation>();
}
