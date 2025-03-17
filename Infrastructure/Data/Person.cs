using System;
using System.Collections.Generic;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class Person
{
    public int Idperson { get; set; }

    public string Firstname { get; set; } = null!;

    public string Lastname { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public DateTime Createdat { get; set; }

    public DateTime? Updatedat { get; set; }

    public DateTime? Lastloginat { get; set; }

    public DateTime? Lastpasswordchangeat { get; set; }

    public bool Isadmin { get; set; }

    public bool Allnotifications { get; set; }

    public int? Cropid { get; set; }

    public virtual ICollection<Changepassword> Changepassword { get; set; } = new List<Changepassword>();

    public virtual ICollection<Crop> Crop { get; set; } = new List<Crop>();

    public virtual Crop? CropNavigation { get; set; }

    public virtual ICollection<Cropinvitation> CropinvitationCreatedbyNavigation { get; set; } = new List<Cropinvitation>();

    public virtual ICollection<Cropinvitation> CropinvitationUsedbyNavigation { get; set; } = new List<Cropinvitation>();

    public virtual ICollection<Devicedata> DevicedataRegisteredbyNavigation { get; set; } = new List<Devicedata>();

    public virtual ICollection<Devicedata> DevicedataUpdatedbyNavigation { get; set; } = new List<Devicedata>();

    public virtual ICollection<Failedloginattempt> Failedloginattempt { get; set; } = new List<Failedloginattempt>();

    public virtual ICollection<Plantstatehistory> Plantstatehistory { get; set; } = new List<Plantstatehistory>();

    public virtual ICollection<Refreshtoken> Refreshtoken { get; set; } = new List<Refreshtoken>();
}
