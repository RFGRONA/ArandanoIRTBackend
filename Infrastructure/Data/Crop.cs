using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Crop
{
    public int Idcrop { get; set; }

    public string Namecrop { get; set; } = null!;

    public string Addrescrop { get; set; } = null!;

    public string Cityname { get; set; } = null!;

    public DateTime Createdat { get; set; }

    public DateTime? Updatedat { get; set; }

    public int? Adminuserid { get; set; }

    public virtual Person? Adminuser { get; set; }

    public virtual ICollection<Cropinvitation> Cropinvitation { get; set; } = new List<Cropinvitation>();

    public virtual ICollection<Devicedata> Devicedata { get; set; } = new List<Devicedata>();

    public virtual ICollection<Person> Person { get; set; } = new List<Person>();

    public virtual ICollection<Plantdata> Plantdata { get; set; } = new List<Plantdata>();

    public virtual ICollection<Sensordata> Sensordata { get; set; } = new List<Sensordata>();

    public virtual ICollection<Thermaldata> Thermaldata { get; set; } = new List<Thermaldata>();
}
