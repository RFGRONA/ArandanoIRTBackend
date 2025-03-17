using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Auditsensitivedata
{
    public int Idauditsensitivedata { get; set; }

    public string Tablename { get; set; } = null!;

    public string Columnname { get; set; } = null!;

    public int Recordid { get; set; }

    public int Cropid { get; set; }

    public string Action { get; set; } = null!;

    public DateTime? Performedat { get; set; }

    public int? Performedby { get; set; }

    public string? Performedbyip { get; set; }

    public string? Useragent { get; set; }
}
