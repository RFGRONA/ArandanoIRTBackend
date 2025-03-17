using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Devicelog
{
    public int Iddevicelog { get; set; }

    public int Deviceid { get; set; }

    public string Logtype { get; set; } = null!;

    public string Logmessage { get; set; } = null!;

    public DateTime Logtimestamp { get; set; }

    public virtual Devicedata Device { get; set; } = null!;
}
