using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Changepassword
{
    public int Idchangepassword { get; set; }

    public string Passwordresettoken { get; set; } = null!;

    public DateTime Resettokenexpiresat { get; set; }

    public DateTime Tokencreatedat { get; set; }

    public int? Personid { get; set; }

    public virtual Person? Person { get; set; }
}
