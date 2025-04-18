using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Devicedata
{
    public int Iddevicedata { get; set; }

    public string? Namedevice { get; set; }

    public string? Descriptiondevice { get; set; }

    public short Datacollectiontime { get; set; }

    public int? Statusid { get; set; }

    public DateTime Registeredat { get; set; }

    public int? Registeredby { get; set; }

    public DateTime? Updatedat { get; set; }

    public int? Updatedby { get; set; }

    public int? Cropid { get; set; }

    public int? Plantid { get; set; }

    public virtual Crop? Crop { get; set; }

    public virtual ICollection<Deviceactivation> Deviceactivation { get; set; } = new List<Deviceactivation>();

    public virtual ICollection<Devicelog> Devicelog { get; set; } = new List<Devicelog>();

    public virtual ICollection<Devicetoken> Devicetoken { get; set; } = new List<Devicetoken>();

    public virtual Plantdata? Plant { get; set; }

    public virtual Person? RegisteredbyNavigation { get; set; }

    public virtual Status? Status { get; set; }

    public virtual Person? UpdatedbyNavigation { get; set; }
}
