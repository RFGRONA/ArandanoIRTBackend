using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Plantstatehistory
{
    public int Idplantstatehistory { get; set; }

    public DateTime Changedat { get; set; }

    public int? Changedby { get; set; }

    public int? Plantid { get; set; }

    public int? Statusid { get; set; }

    public virtual Person? ChangedbyNavigation { get; set; }

    public virtual Plantdata? Plant { get; set; }

    public virtual Status? Status { get; set; }
}
