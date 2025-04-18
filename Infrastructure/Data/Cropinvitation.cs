using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Cropinvitation
{
    public int Idcropinvitation { get; set; }

    public string Accescode { get; set; } = null!;

    public DateTime Createdat { get; set; }

    public DateTime Expiresat { get; set; }

    public int? Statusid { get; set; }

    public int? Createdby { get; set; }

    public int? Usedby { get; set; }

    public int? Cropid { get; set; }

    public virtual Person? CreatedbyNavigation { get; set; }

    public virtual Crop? Crop { get; set; }

    public virtual Status? Status { get; set; }

    public virtual Person? UsedbyNavigation { get; set; }
}
