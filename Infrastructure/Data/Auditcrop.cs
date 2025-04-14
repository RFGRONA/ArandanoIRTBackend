using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Auditcrop
{
    public int Idauditcrop { get; set; }

    public string Columnname { get; set; } = null!;

    public int Recordid { get; set; }

    public int? Cropid { get; set; }

    public string Action { get; set; } = null!;

    public string? Oldvalue { get; set; }

    public string? Newvalue { get; set; }

    public DateTime? Performedat { get; set; }

    public int? Performedby { get; set; }

    public string? Performedbyip { get; set; }

    public string? Useragent { get; set; }
}
