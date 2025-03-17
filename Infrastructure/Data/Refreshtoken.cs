using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Refreshtoken
{
    public int Idrefreshtoken { get; set; }

    public long Session { get; set; }

    public string Refreshtoken1 { get; set; } = null!;

    public string Deviceinfo { get; set; } = null!;

    public string Ipaddress { get; set; } = null!;

    public string Useragent { get; set; } = null!;

    public DateTime Createdat { get; set; }

    public DateTime Expiresat { get; set; }

    public DateTime? Revokedat { get; set; }

    public string? Revokedbyip { get; set; }

    public string? Replacedbytoken { get; set; }

    public int? Personid { get; set; }

    public virtual Person? Person { get; set; }
}
