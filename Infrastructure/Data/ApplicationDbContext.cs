using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ArandanoIRT_Backend.Infrastructure.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Auditcrop> Auditcrop { get; set; }

    public virtual DbSet<Auditdatatable> Auditdatatable { get; set; }

    public virtual DbSet<Auditdevice> Auditdevice { get; set; }

    public virtual DbSet<Auditperson> Auditperson { get; set; }

    public virtual DbSet<Auditsensitivedata> Auditsensitivedata { get; set; }

    public virtual DbSet<Auditsystemtable> Auditsystemtable { get; set; }

    public virtual DbSet<Changepassword> Changepassword { get; set; }

    public virtual DbSet<Crop> Crop { get; set; }

    public virtual DbSet<Cropinvitation> Cropinvitation { get; set; }

    public virtual DbSet<Deviceactivation> Deviceactivation { get; set; }

    public virtual DbSet<Devicedata> Devicedata { get; set; }

    public virtual DbSet<Devicelog> Devicelog { get; set; }

    public virtual DbSet<Devicetoken> Devicetoken { get; set; }

    public virtual DbSet<Failedloginattempt> Failedloginattempt { get; set; }

    public virtual DbSet<Person> Person { get; set; }

    public virtual DbSet<Plantdata> Plantdata { get; set; }

    public virtual DbSet<Plantobservation> Plantobservation { get; set; }

    public virtual DbSet<Plantstatehistory> Plantstatehistory { get; set; }

    public virtual DbSet<Refreshtoken> Refreshtoken { get; set; }

    public virtual DbSet<Sensordata> Sensordata { get; set; }

    public virtual DbSet<Status> Status { get; set; }

    public virtual DbSet<Tablerelation> Tablerelation { get; set; }

    public virtual DbSet<Thermaldata> Thermaldata { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Name=ConnectionString");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Auditcrop>(entity =>
        {
            entity.HasKey(e => e.Idauditcrop).HasName("auditcrop_pkey");

            entity.ToTable("auditcrop");

            entity.HasIndex(e => e.Cropid, "auditcrop_cropid");

            entity.HasIndex(e => e.Performedat, "auditcrop_performedat");

            entity.Property(e => e.Idauditcrop).HasColumnName("idauditcrop");
            entity.Property(e => e.Action)
                .HasMaxLength(20)
                .HasColumnName("action");
            entity.Property(e => e.Columnname)
                .HasMaxLength(50)
                .HasColumnName("columnname");
            entity.Property(e => e.Cropid).HasColumnName("cropid");
            entity.Property(e => e.Newvalue).HasColumnName("newvalue");
            entity.Property(e => e.Oldvalue).HasColumnName("oldvalue");
            entity.Property(e => e.Performedat).HasColumnName("performedat");
            entity.Property(e => e.Performedby).HasColumnName("performedby");
            entity.Property(e => e.Performedbyip)
                .HasMaxLength(45)
                .HasColumnName("performedbyip");
            entity.Property(e => e.Recordid).HasColumnName("recordid");
            entity.Property(e => e.Useragent).HasColumnName("useragent");
        });

        modelBuilder.Entity<Auditdatatable>(entity =>
        {
            entity.HasKey(e => e.Idauditdatatable).HasName("auditdatatable_pkey");

            entity.ToTable("auditdatatable");

            entity.HasIndex(e => e.Cropid, "auditdatatable_cropid");

            entity.HasIndex(e => e.Performedat, "auditdatatable_performedat");

            entity.HasIndex(e => e.Tablename, "auditdatatable_tablename");

            entity.Property(e => e.Idauditdatatable).HasColumnName("idauditdatatable");
            entity.Property(e => e.Action)
                .HasMaxLength(20)
                .HasColumnName("action");
            entity.Property(e => e.Columnname)
                .HasMaxLength(50)
                .HasColumnName("columnname");
            entity.Property(e => e.Cropid).HasColumnName("cropid");
            entity.Property(e => e.Newvalue).HasColumnName("newvalue");
            entity.Property(e => e.Oldvalue).HasColumnName("oldvalue");
            entity.Property(e => e.Performedat).HasColumnName("performedat");
            entity.Property(e => e.Performedby).HasColumnName("performedby");
            entity.Property(e => e.Performedbyip)
                .HasMaxLength(45)
                .HasColumnName("performedbyip");
            entity.Property(e => e.Recordid).HasColumnName("recordid");
            entity.Property(e => e.Tablename)
                .HasMaxLength(50)
                .HasColumnName("tablename");
            entity.Property(e => e.Useragent).HasColumnName("useragent");
        });

        modelBuilder.Entity<Auditdevice>(entity =>
        {
            entity.HasKey(e => e.Idauditdevice).HasName("auditdevice_pkey");

            entity.ToTable("auditdevice");

            entity.HasIndex(e => e.Cropid, "auditdevice_cropid");

            entity.HasIndex(e => e.Performedat, "auditdevice_performedat");

            entity.Property(e => e.Idauditdevice).HasColumnName("idauditdevice");
            entity.Property(e => e.Action)
                .HasMaxLength(20)
                .HasColumnName("action");
            entity.Property(e => e.Columnname)
                .HasMaxLength(50)
                .HasColumnName("columnname");
            entity.Property(e => e.Cropid).HasColumnName("cropid");
            entity.Property(e => e.Newvalue).HasColumnName("newvalue");
            entity.Property(e => e.Oldvalue).HasColumnName("oldvalue");
            entity.Property(e => e.Performedat).HasColumnName("performedat");
            entity.Property(e => e.Performedby).HasColumnName("performedby");
            entity.Property(e => e.Performedbyip)
                .HasMaxLength(45)
                .HasColumnName("performedbyip");
            entity.Property(e => e.Recordid).HasColumnName("recordid");
            entity.Property(e => e.Useragent).HasColumnName("useragent");
        });

        modelBuilder.Entity<Auditperson>(entity =>
        {
            entity.HasKey(e => e.Idauditperson).HasName("auditperson_pkey");

            entity.ToTable("auditperson");

            entity.HasIndex(e => e.Cropid, "auditperson_cropid");

            entity.HasIndex(e => e.Performedat, "auditperson_performedat");

            entity.Property(e => e.Idauditperson).HasColumnName("idauditperson");
            entity.Property(e => e.Action)
                .HasMaxLength(20)
                .HasColumnName("action");
            entity.Property(e => e.Columnname)
                .HasMaxLength(50)
                .HasColumnName("columnname");
            entity.Property(e => e.Cropid).HasColumnName("cropid");
            entity.Property(e => e.Performedat).HasColumnName("performedat");
            entity.Property(e => e.Performedby).HasColumnName("performedby");
            entity.Property(e => e.Performedbyip)
                .HasMaxLength(45)
                .HasColumnName("performedbyip");
            entity.Property(e => e.Recordid).HasColumnName("recordid");
            entity.Property(e => e.Useragent).HasColumnName("useragent");
        });

        modelBuilder.Entity<Auditsensitivedata>(entity =>
        {
            entity.HasKey(e => e.Idauditsensitivedata).HasName("auditsensitivedata_pkey");

            entity.ToTable("auditsensitivedata");

            entity.HasIndex(e => e.Cropid, "auditsensitivedata_cropid");

            entity.HasIndex(e => e.Performedat, "auditsensitivedata_performedat");

            entity.HasIndex(e => e.Tablename, "auditsensitivedata_tablename");

            entity.Property(e => e.Idauditsensitivedata).HasColumnName("idauditsensitivedata");
            entity.Property(e => e.Action)
                .HasMaxLength(20)
                .HasColumnName("action");
            entity.Property(e => e.Columnname)
                .HasMaxLength(50)
                .HasColumnName("columnname");
            entity.Property(e => e.Cropid).HasColumnName("cropid");
            entity.Property(e => e.Performedat).HasColumnName("performedat");
            entity.Property(e => e.Performedby).HasColumnName("performedby");
            entity.Property(e => e.Performedbyip)
                .HasMaxLength(45)
                .HasColumnName("performedbyip");
            entity.Property(e => e.Recordid).HasColumnName("recordid");
            entity.Property(e => e.Tablename)
                .HasMaxLength(50)
                .HasColumnName("tablename");
            entity.Property(e => e.Useragent).HasColumnName("useragent");
        });

        modelBuilder.Entity<Auditsystemtable>(entity =>
        {
            entity.HasKey(e => e.Idauditsystemtable).HasName("auditsystemtable_pkey");

            entity.ToTable("auditsystemtable");

            entity.HasIndex(e => e.Cropid, "auditsystemtable_cropid");

            entity.HasIndex(e => e.Performedat, "auditsystemtable_performedat");

            entity.HasIndex(e => e.Tablename, "auditsystemtable_tablename");

            entity.Property(e => e.Idauditsystemtable).HasColumnName("idauditsystemtable");
            entity.Property(e => e.Action)
                .HasMaxLength(20)
                .HasColumnName("action");
            entity.Property(e => e.Columnname)
                .HasMaxLength(50)
                .HasColumnName("columnname");
            entity.Property(e => e.Cropid).HasColumnName("cropid");
            entity.Property(e => e.Newvalue).HasColumnName("newvalue");
            entity.Property(e => e.Oldvalue).HasColumnName("oldvalue");
            entity.Property(e => e.Performedat).HasColumnName("performedat");
            entity.Property(e => e.Performedby).HasColumnName("performedby");
            entity.Property(e => e.Performedbyip)
                .HasMaxLength(45)
                .HasColumnName("performedbyip");
            entity.Property(e => e.Recordid).HasColumnName("recordid");
            entity.Property(e => e.Tablename)
                .HasMaxLength(50)
                .HasColumnName("tablename");
            entity.Property(e => e.Useragent).HasColumnName("useragent");
        });

        modelBuilder.Entity<Changepassword>(entity =>
        {
            entity.HasKey(e => e.Idchangepassword).HasName("changepassword_pkey");

            entity.ToTable("changepassword");

            entity.HasIndex(e => e.Passwordresettoken, "changepassword_passwordresettoken");

            entity.HasIndex(e => e.Personid, "changepassword_personid");

            entity.Property(e => e.Idchangepassword).HasColumnName("idchangepassword");
            entity.Property(e => e.Passwordresettoken).HasColumnName("passwordresettoken");
            entity.Property(e => e.Personid).HasColumnName("personid");
            entity.Property(e => e.Resettokenexpiresat).HasColumnName("resettokenexpiresat");
            entity.Property(e => e.Tokencreatedat).HasColumnName("tokencreatedat");

            entity.HasOne(d => d.Person).WithMany(p => p.Changepassword)
                .HasForeignKey(d => d.Personid)
                .HasConstraintName("changepassword_personid_fkey");
        });

        modelBuilder.Entity<Crop>(entity =>
        {
            entity.HasKey(e => e.Idcrop).HasName("crop_pkey");

            entity.ToTable("crop");

            entity.HasIndex(e => e.Adminuserid, "crop_adminuserid");

            entity.Property(e => e.Idcrop).HasColumnName("idcrop");
            entity.Property(e => e.Addresscrop)
                .HasMaxLength(80)
                .HasColumnName("addresscrop");
            entity.Property(e => e.Adminuserid).HasColumnName("adminuserid");
            entity.Property(e => e.Cityname)
                .HasMaxLength(124)
                .HasColumnName("cityname");
            entity.Property(e => e.Createdat).HasColumnName("createdat");
            entity.Property(e => e.Namecrop)
                .HasMaxLength(50)
                .HasColumnName("namecrop");
            entity.Property(e => e.Updatedat).HasColumnName("updatedat");

            entity.HasOne(d => d.Adminuser).WithMany(p => p.Crop)
                .HasForeignKey(d => d.Adminuserid)
                .HasConstraintName("crop_adminuserid_fkey");
        });

        modelBuilder.Entity<Cropinvitation>(entity =>
        {
            entity.HasKey(e => e.Idcropinvitation).HasName("cropinvitation_pkey");

            entity.ToTable("cropinvitation");

            entity.HasIndex(e => e.Accescode, "cropinvitation_accescode");

            entity.HasIndex(e => e.Createdby, "cropinvitation_createdby");

            entity.HasIndex(e => e.Cropid, "cropinvitation_cropid");

            entity.HasIndex(e => e.Statusid, "cropinvitation_statusid");

            entity.HasIndex(e => e.Usedby, "cropinvitation_usedby");

            entity.Property(e => e.Idcropinvitation).HasColumnName("idcropinvitation");
            entity.Property(e => e.Accescode)
                .HasMaxLength(255)
                .HasColumnName("accescode");
            entity.Property(e => e.Createdat).HasColumnName("createdat");
            entity.Property(e => e.Createdby).HasColumnName("createdby");
            entity.Property(e => e.Cropid).HasColumnName("cropid");
            entity.Property(e => e.Expiresat).HasColumnName("expiresat");
            entity.Property(e => e.Statusid).HasColumnName("statusid");
            entity.Property(e => e.Usedby).HasColumnName("usedby");

            entity.HasOne(d => d.CreatedbyNavigation).WithMany(p => p.CropinvitationCreatedbyNavigation)
                .HasForeignKey(d => d.Createdby)
                .HasConstraintName("cropinvitation_createdby_fkey");

            entity.HasOne(d => d.Crop).WithMany(p => p.Cropinvitation)
                .HasForeignKey(d => d.Cropid)
                .HasConstraintName("cropinvitation_cropid_fkey");

            entity.HasOne(d => d.Status).WithMany(p => p.Cropinvitation)
                .HasForeignKey(d => d.Statusid)
                .HasConstraintName("cropinvitation_statusid_fkey");

            entity.HasOne(d => d.UsedbyNavigation).WithMany(p => p.CropinvitationUsedbyNavigation)
                .HasForeignKey(d => d.Usedby)
                .HasConstraintName("cropinvitation_usedby_fkey");
        });

        modelBuilder.Entity<Deviceactivation>(entity =>
        {
            entity.HasKey(e => e.Iddeviceactivation).HasName("deviceactivation_pkey");

            entity.ToTable("deviceactivation");

            entity.HasIndex(e => e.Activationstatus, "deviceactivation_activationstatus");

            entity.HasIndex(e => e.Deviceid, "deviceactivation_deviceid");

            entity.Property(e => e.Iddeviceactivation).HasColumnName("iddeviceactivation");
            entity.Property(e => e.Activatedat).HasColumnName("activatedat");
            entity.Property(e => e.Activationcode).HasColumnName("activationcode");
            entity.Property(e => e.Activationstatus).HasColumnName("activationstatus");
            entity.Property(e => e.Createdat).HasColumnName("createdat");
            entity.Property(e => e.Deviceid).HasColumnName("deviceid");
            entity.Property(e => e.Expiresat).HasColumnName("expiresat");

            entity.HasOne(d => d.ActivationstatusNavigation).WithMany(p => p.Deviceactivation)
                .HasForeignKey(d => d.Activationstatus)
                .HasConstraintName("deviceactivation_activationstatus_fkey");

            entity.HasOne(d => d.Device).WithMany(p => p.Deviceactivation)
                .HasForeignKey(d => d.Deviceid)
                .HasConstraintName("deviceactivation_deviceid_fkey");
        });

        modelBuilder.Entity<Devicedata>(entity =>
        {
            entity.HasKey(e => e.Iddevicedata).HasName("devicedata_pkey");

            entity.ToTable("devicedata");

            entity.HasIndex(e => e.Cropid, "devicedata_cropid");

            entity.HasIndex(e => e.Plantid, "devicedata_plantid");

            entity.HasIndex(e => e.Registeredby, "devicedata_registeredby");

            entity.HasIndex(e => e.Statusid, "devicedata_statusid");

            entity.HasIndex(e => e.Updatedby, "devicedata_updatedby");

            entity.Property(e => e.Iddevicedata).HasColumnName("iddevicedata");
            entity.Property(e => e.Cropid).HasColumnName("cropid");
            entity.Property(e => e.Datacollectiontime).HasColumnName("datacollectiontime");
            entity.Property(e => e.Descriptiondevice).HasColumnName("descriptiondevice");
            entity.Property(e => e.Namedevice)
                .HasMaxLength(50)
                .HasColumnName("namedevice");
            entity.Property(e => e.Plantid).HasColumnName("plantid");
            entity.Property(e => e.Registeredat).HasColumnName("registeredat");
            entity.Property(e => e.Registeredby).HasColumnName("registeredby");
            entity.Property(e => e.Statusid).HasColumnName("statusid");
            entity.Property(e => e.Updatedat).HasColumnName("updatedat");
            entity.Property(e => e.Updatedby).HasColumnName("updatedby");

            entity.HasOne(d => d.Crop).WithMany(p => p.Devicedata)
                .HasForeignKey(d => d.Cropid)
                .HasConstraintName("devicedata_cropid_fkey");

            entity.HasOne(d => d.Plant).WithMany(p => p.Devicedata)
                .HasForeignKey(d => d.Plantid)
                .HasConstraintName("devicedata_plantid_fkey");

            entity.HasOne(d => d.RegisteredbyNavigation).WithMany(p => p.DevicedataRegisteredbyNavigation)
                .HasForeignKey(d => d.Registeredby)
                .HasConstraintName("devicedata_registeredby_fkey");

            entity.HasOne(d => d.Status).WithMany(p => p.Devicedata)
                .HasForeignKey(d => d.Statusid)
                .HasConstraintName("devicedata_statusid_fkey");

            entity.HasOne(d => d.UpdatedbyNavigation).WithMany(p => p.DevicedataUpdatedbyNavigation)
                .HasForeignKey(d => d.Updatedby)
                .HasConstraintName("devicedata_updatedby_fkey");
        });

        modelBuilder.Entity<Devicelog>(entity =>
        {
            entity.HasKey(e => e.Iddevicelog).HasName("devicelog_pkey");

            entity.ToTable("devicelog");

            entity.Property(e => e.Iddevicelog).HasColumnName("iddevicelog");
            entity.Property(e => e.Deviceid).HasColumnName("deviceid");
            entity.Property(e => e.Logmessage).HasColumnName("logmessage");
            entity.Property(e => e.Logtimestamp)
                .HasDefaultValueSql("now()")
                .HasColumnName("logtimestamp");
            entity.Property(e => e.Logtype)
                .HasMaxLength(50)
                .HasColumnName("logtype");

            entity.HasOne(d => d.Device).WithMany(p => p.Devicelog)
                .HasForeignKey(d => d.Deviceid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_devicelog_devicedata");
        });

        modelBuilder.Entity<Devicetoken>(entity =>
        {
            entity.HasKey(e => e.Iddevicetoken).HasName("devicetoken_pkey");

            entity.ToTable("devicetoken");

            entity.HasIndex(e => e.Deviceid, "devicetoken_deviceid");

            entity.HasIndex(e => e.Token, "devicetoken_token");

            entity.Property(e => e.Iddevicetoken).HasColumnName("iddevicetoken");
            entity.Property(e => e.Createdat).HasColumnName("createdat");
            entity.Property(e => e.Deviceid).HasColumnName("deviceid");
            entity.Property(e => e.Deviceinfo).HasColumnName("deviceinfo");
            entity.Property(e => e.Expiresat).HasColumnName("expiresat");
            entity.Property(e => e.Refreshtoken).HasColumnName("refreshtoken");
            entity.Property(e => e.Revokedat).HasColumnName("revokedat");
            entity.Property(e => e.Revokedbyip)
                .HasMaxLength(45)
                .HasColumnName("revokedbyip");
            entity.Property(e => e.Token).HasColumnName("token");
            entity.Property(e => e.Useragent).HasColumnName("useragent");

            entity.HasOne(d => d.Device).WithMany(p => p.Devicetoken)
                .HasForeignKey(d => d.Deviceid)
                .HasConstraintName("devicetoken_deviceid_fkey");
        });

        modelBuilder.Entity<Failedloginattempt>(entity =>
        {
            entity.HasKey(e => e.Idfailedloginattempt).HasName("failedloginattempt_pkey");

            entity.ToTable("failedloginattempt");

            entity.HasIndex(e => e.Attemptdate, "failedloginattempt_attemptdate");

            entity.HasIndex(e => e.Personid, "failedloginattempt_personid");

            entity.Property(e => e.Idfailedloginattempt).HasColumnName("idfailedloginattempt");
            entity.Property(e => e.Attemptdate).HasColumnName("attemptdate");
            entity.Property(e => e.Deviceinfo).HasColumnName("deviceinfo");
            entity.Property(e => e.Ipaddress)
                .HasMaxLength(45)
                .HasColumnName("ipaddress");
            entity.Property(e => e.Personid).HasColumnName("personid");
            entity.Property(e => e.Useragent).HasColumnName("useragent");

            entity.HasOne(d => d.Person).WithMany(p => p.Failedloginattempt)
                .HasForeignKey(d => d.Personid)
                .HasConstraintName("failedloginattempt_personid_fkey");
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.HasKey(e => e.Idperson).HasName("person_pkey");

            entity.ToTable("person");

            entity.HasIndex(e => e.Cropid, "person_cropid");

            entity.HasIndex(e => e.Email, "person_email_key").IsUnique();

            entity.Property(e => e.Idperson).HasColumnName("idperson");
            entity.Property(e => e.Allnotifications)
                .HasDefaultValue(true)
                .HasColumnName("allnotifications");
            entity.Property(e => e.Createdat).HasColumnName("createdat");
            entity.Property(e => e.Cropid).HasColumnName("cropid");
            entity.Property(e => e.Email)
                .HasMaxLength(75)
                .HasColumnName("email");
            entity.Property(e => e.Firstname)
                .HasMaxLength(40)
                .HasColumnName("firstname");
            entity.Property(e => e.Isadmin)
                .HasDefaultValue(false)
                .HasColumnName("isadmin");
            entity.Property(e => e.Lastloginat).HasColumnName("lastloginat");
            entity.Property(e => e.Lastname)
                .HasMaxLength(40)
                .HasColumnName("lastname");
            entity.Property(e => e.Lastpasswordchangeat).HasColumnName("lastpasswordchangeat");
            entity.Property(e => e.Password).HasColumnName("password");
            entity.Property(e => e.Updatedat).HasColumnName("updatedat");

            entity.HasOne(d => d.CropNavigation).WithMany(p => p.Person)
                .HasForeignKey(d => d.Cropid)
                .HasConstraintName("fk_person_crop");
        });

        modelBuilder.Entity<Plantdata>(entity =>
        {
            entity.HasKey(e => e.Idplantdata).HasName("plantdata_pkey");

            entity.ToTable("plantdata");

            entity.HasIndex(e => e.Cropid, "plantdata_cropid");

            entity.HasIndex(e => e.Statusid, "plantdata_statusid");

            entity.Property(e => e.Idplantdata).HasColumnName("idplantdata");
            entity.Property(e => e.Cropid).HasColumnName("cropid");
            entity.Property(e => e.Nameplant)
                .HasMaxLength(30)
                .HasColumnName("nameplant");
            entity.Property(e => e.Registeredat).HasColumnName("registeredat");
            entity.Property(e => e.Statusid).HasColumnName("statusid");
            entity.Property(e => e.Updatedat).HasColumnName("updatedat");

            entity.HasOne(d => d.Crop).WithMany(p => p.Plantdata)
                .HasForeignKey(d => d.Cropid)
                .HasConstraintName("plantdata_cropid_fkey");

            entity.HasOne(d => d.Status).WithMany(p => p.Plantdata)
                .HasForeignKey(d => d.Statusid)
                .HasConstraintName("plantdata_statusid_fkey");
        });

        modelBuilder.Entity<Plantobservation>(entity =>
        {
            entity.HasKey(e => e.Idobservation).HasName("plantobservation_pkey");

            entity.ToTable("plantobservation");

            entity.HasIndex(e => e.Createdat, "plantobservation_createdat");

            entity.HasIndex(e => e.Createdby, "plantobservation_createdby");

            entity.HasIndex(e => e.Plantid, "plantobservation_plantid");

            entity.HasIndex(e => e.Statusid, "plantobservation_statusid");

            entity.Property(e => e.Idobservation).HasColumnName("idobservation");
            entity.Property(e => e.Additionalnotes).HasColumnName("additionalnotes");
            entity.Property(e => e.Createdat).HasColumnName("createdat");
            entity.Property(e => e.Createdby).HasColumnName("createdby");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Hasaltereduniformity).HasColumnName("hasaltereduniformity");
            entity.Property(e => e.Hasdecolorations).HasColumnName("hasdecolorations");
            entity.Property(e => e.Lastsensordataid).HasColumnName("lastsensordataid");
            entity.Property(e => e.Lastthermaldataid).HasColumnName("lastthermaldataid");
            entity.Property(e => e.Leafstemnotes).HasColumnName("leafstemnotes");
            entity.Property(e => e.Plantid).HasColumnName("plantid");
            entity.Property(e => e.Statusid).HasColumnName("statusid");
            entity.Property(e => e.Subjectiverating).HasColumnName("subjectiverating");

            entity.HasOne(d => d.CreatedbyNavigation).WithMany(p => p.Plantobservation)
                .HasForeignKey(d => d.Createdby)
                .HasConstraintName("plantobservation_createdby_fkey");

            entity.HasOne(d => d.Lastsensordata).WithMany(p => p.Plantobservation)
                .HasForeignKey(d => d.Lastsensordataid)
                .HasConstraintName("plantobservation_lastsensordataid_fkey");

            entity.HasOne(d => d.Lastthermaldata).WithMany(p => p.Plantobservation)
                .HasForeignKey(d => d.Lastthermaldataid)
                .HasConstraintName("plantobservation_lastthermaldataid_fkey");

            entity.HasOne(d => d.Plant).WithMany(p => p.Plantobservation)
                .HasForeignKey(d => d.Plantid)
                .HasConstraintName("plantobservation_plantid_fkey");

            entity.HasOne(d => d.Status).WithMany(p => p.Plantobservation)
                .HasForeignKey(d => d.Statusid)
                .HasConstraintName("plantobservation_statusid_fkey");
        });

        modelBuilder.Entity<Plantstatehistory>(entity =>
        {
            entity.HasKey(e => e.Idplantdatahistory).HasName("plantstatehistory_pkey");

            entity.ToTable("plantstatehistory");

            entity.HasIndex(e => e.Changedby, "plantstatehistory_changedby");

            entity.HasIndex(e => e.Plantid, "plantstatehistory_plantid");

            entity.HasIndex(e => e.Statusid, "plantstatehistory_statusid");

            entity.Property(e => e.Idplantdatahistory).HasColumnName("idplantdatahistory");
            entity.Property(e => e.Changedat).HasColumnName("changedat");
            entity.Property(e => e.Changedby).HasColumnName("changedby");
            entity.Property(e => e.Plantid).HasColumnName("plantid");
            entity.Property(e => e.Statusid).HasColumnName("statusid");

            entity.HasOne(d => d.ChangedbyNavigation).WithMany(p => p.Plantstatehistory)
                .HasForeignKey(d => d.Changedby)
                .HasConstraintName("plantstatehistory_changedby_fkey");

            entity.HasOne(d => d.Plant).WithMany(p => p.Plantstatehistory)
                .HasForeignKey(d => d.Plantid)
                .HasConstraintName("plantstatehistory_plantid_fkey");

            entity.HasOne(d => d.Status).WithMany(p => p.Plantstatehistory)
                .HasForeignKey(d => d.Statusid)
                .HasConstraintName("plantstatehistory_statusid_fkey");
        });

        modelBuilder.Entity<Refreshtoken>(entity =>
        {
            entity.HasKey(e => e.Idrefreshtoken).HasName("refreshtoken_pkey");

            entity.ToTable("refreshtoken");

            entity.HasIndex(e => e.Personid, "refreshtoken_personid");

            entity.HasIndex(e => e.Session, "refreshtoken_session");

            entity.Property(e => e.Idrefreshtoken).HasColumnName("idrefreshtoken");
            entity.Property(e => e.Createdat).HasColumnName("createdat");
            entity.Property(e => e.Deviceinfo).HasColumnName("deviceinfo");
            entity.Property(e => e.Expiresat).HasColumnName("expiresat");
            entity.Property(e => e.Ipaddress)
                .HasMaxLength(45)
                .HasColumnName("ipaddress");
            entity.Property(e => e.Personid).HasColumnName("personid");
            entity.Property(e => e.Refreshtoken1).HasColumnName("refreshtoken");
            entity.Property(e => e.Replacedbytoken).HasColumnName("replacedbytoken");
            entity.Property(e => e.Revokedat).HasColumnName("revokedat");
            entity.Property(e => e.Revokedbyip)
                .HasMaxLength(45)
                .HasColumnName("revokedbyip");
            entity.Property(e => e.Session).HasColumnName("session");
            entity.Property(e => e.Useragent).HasColumnName("useragent");

            entity.HasOne(d => d.Person).WithMany(p => p.Refreshtoken)
                .HasForeignKey(d => d.Personid)
                .HasConstraintName("refreshtoken_personid_fkey");
        });

        modelBuilder.Entity<Sensordata>(entity =>
        {
            entity.HasKey(e => e.Idsensordata).HasName("sensordata_pkey");

            entity.ToTable("sensordata");

            entity.HasIndex(e => e.Cropid, "sensordata_cropid");

            entity.HasIndex(e => e.Plantid, "sensordata_plantid");

            entity.Property(e => e.Idsensordata).HasColumnName("idsensordata");
            entity.Property(e => e.Cityhumidity).HasColumnName("cityhumidity");
            entity.Property(e => e.Citytemperature).HasColumnName("citytemperature");
            entity.Property(e => e.Cropid).HasColumnName("cropid");
            entity.Property(e => e.Humidity).HasColumnName("humidity");
            entity.Property(e => e.Lightintensity).HasColumnName("lightintensity");
            entity.Property(e => e.Plantid).HasColumnName("plantid");
            entity.Property(e => e.Recordedat).HasColumnName("recordedat");
            entity.Property(e => e.Temperature).HasColumnName("temperature");

            entity.HasOne(d => d.Crop).WithMany(p => p.Sensordata)
                .HasForeignKey(d => d.Cropid)
                .HasConstraintName("sensordata_cropid_fkey");

            entity.HasOne(d => d.Plant).WithMany(p => p.Sensordata)
                .HasForeignKey(d => d.Plantid)
                .HasConstraintName("sensordata_plantid_fkey");
        });

        modelBuilder.Entity<Status>(entity =>
        {
            entity.HasKey(e => e.Idstatus).HasName("status_pkey");

            entity.ToTable("status");

            entity.Property(e => e.Idstatus).HasColumnName("idstatus");
            entity.Property(e => e.Namestatus)
                .HasMaxLength(25)
                .HasColumnName("namestatus");
            entity.Property(e => e.Tablerelationid).HasColumnName("tablerelationid");

            entity.HasOne(d => d.Tablerelation).WithMany(p => p.Status)
                .HasForeignKey(d => d.Tablerelationid)
                .HasConstraintName("fk_status_tablerelation");
        });

        modelBuilder.Entity<Tablerelation>(entity =>
        {
            entity.HasKey(e => e.Idtablerelation).HasName("tablerelation_pkey");

            entity.ToTable("tablerelation");

            entity.Property(e => e.Idtablerelation).HasColumnName("idtablerelation");
            entity.Property(e => e.Tablename)
                .HasMaxLength(50)
                .HasColumnName("tablename");
        });

        modelBuilder.Entity<Thermaldata>(entity =>
        {
            entity.HasKey(e => e.Idthermaldata).HasName("thermaldata_pkey");

            entity.ToTable("thermaldata");

            entity.HasIndex(e => e.Cropid, "thermaldata_cropid");

            entity.HasIndex(e => e.Plantid, "thermaldata_plantid");

            entity.Property(e => e.Idthermaldata).HasColumnName("idthermaldata");
            entity.Property(e => e.Cropid).HasColumnName("cropid");
            entity.Property(e => e.Plantid).HasColumnName("plantid");
            entity.Property(e => e.Recordedat).HasColumnName("recordedat");
            entity.Property(e => e.Rgbimagedata).HasColumnName("rgbimagedata");
            entity.Property(e => e.Thermalimagedata)
                .HasColumnType("jsonb")
                .HasColumnName("thermalimagedata");

            entity.HasOne(d => d.Crop).WithMany(p => p.Thermaldata)
                .HasForeignKey(d => d.Cropid)
                .HasConstraintName("thermaldata_cropid_fkey");

            entity.HasOne(d => d.Plant).WithMany(p => p.Thermaldata)
                .HasForeignKey(d => d.Plantid)
                .HasConstraintName("thermaldata_plantid_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
