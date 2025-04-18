using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Devicetoken
{
    public int Iddevicetoken { get; set; }

    public int? Deviceid { get; set; }

    public string Token { get; set; } = null!;

    public string Refreshtoken { get; set; } = null!;

    public DateTime Createdat { get; set; }

    public DateTime Expiresat { get; set; }

    public DateTime? Revokedat { get; set; }

    public string? Revokedbyip { get; set; }

    public string? Deviceinfo { get; set; }

    public string? Useragent { get; set; }

    public virtual Devicedata? Device { get; set; }
}
