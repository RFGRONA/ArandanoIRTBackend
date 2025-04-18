using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Tablerelation
{
    public int Idtablerelation { get; set; }

    public string Tablename { get; set; } = null!;

    public virtual ICollection<Status> Status { get; set; } = new List<Status>();
}
