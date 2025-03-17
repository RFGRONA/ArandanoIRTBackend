using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Failedloginattempt
{
    public int Idfailedloginattempt { get; set; }

    public DateTime Attemptdate { get; set; }

    public string Ipaddress { get; set; } = null!;

    public string Deviceinfo { get; set; } = null!;

    public string Useragent { get; set; } = null!;

    public int? Personid { get; set; }

    public virtual Person? Person { get; set; }
}
