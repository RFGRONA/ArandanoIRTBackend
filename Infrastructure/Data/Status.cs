using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Status
{
    public int Idstatus { get; set; }

    public string Namestatus { get; set; } = null!;

    public int? Tablerelationid { get; set; }

    public virtual ICollection<Cropinvitation> Cropinvitation { get; set; } = new List<Cropinvitation>();

    public virtual ICollection<Deviceactivation> Deviceactivation { get; set; } = new List<Deviceactivation>();

    public virtual ICollection<Devicedata> Devicedata { get; set; } = new List<Devicedata>();

    public virtual ICollection<Plantdata> Plantdata { get; set; } = new List<Plantdata>();

    public virtual ICollection<Plantstatehistory> Plantstatehistory { get; set; } = new List<Plantstatehistory>();

    public virtual Tablerelation? Tablerelation { get; set; }
}
