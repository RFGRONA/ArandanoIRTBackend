using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Deviceactivation
{
    public int Iddeviceactivation { get; set; }

    public int? Deviceid { get; set; }

    public string Activationcode { get; set; } = null!;

    public DateTime Createdat { get; set; }

    public DateTime Expiresat { get; set; }

    public DateTime? Activatedat { get; set; }

    public int? Activationstatus { get; set; }

    public virtual Status? ActivationstatusNavigation { get; set; }

    public virtual Devicedata? Device { get; set; }
}
