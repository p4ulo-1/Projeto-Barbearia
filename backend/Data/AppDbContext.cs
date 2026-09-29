using BackAndre.Models;
using Microsoft.EntityFrameworkCore;

namespace BackAndre.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Barber> Barbers => Set<Barber>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(user => user.Id).HasMaxLength(32);
            entity.Property(user => user.Name).HasMaxLength(100).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(150).UseCollation("NOCASE").IsRequired();
            entity.Property(user => user.Phone).HasMaxLength(30).IsRequired();
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(20).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<Service>(entity =>
        {
            entity.Property(service => service.Id).HasMaxLength(50);
            entity.Property(service => service.Name).HasMaxLength(100).IsRequired();
            entity.Property(service => service.Description).HasMaxLength(500).IsRequired();
            entity.Property(service => service.Price).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Barber>(entity =>
        {
            entity.Property(barber => barber.Id).HasMaxLength(50);
            entity.Property(barber => barber.Name).HasMaxLength(100).IsRequired();
            entity.Property(barber => barber.Role).HasMaxLength(100).IsRequired();
            entity.Property(barber => barber.Bio).HasMaxLength(500).IsRequired();
            entity.OwnsOne(barber => barber.Schedule, schedule =>
            {
                schedule.Property(item => item.WorkDays)
                    .HasColumnName("WorkDays")
                    .HasMaxLength(20)
                    .IsRequired();
                schedule.Property(item => item.StartTime).HasColumnName("StartTime");
                schedule.Property(item => item.EndTime).HasColumnName("EndTime");
                schedule.Ignore(item => item.WorkDayNumbers);
            });
            entity.Navigation(barber => barber.Schedule).IsRequired();
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.Property(appointment => appointment.Id).HasMaxLength(32);
            entity.Property(appointment => appointment.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(appointment => new
            {
                appointment.BarberId,
                appointment.Date,
                appointment.Time
            });
            entity.HasOne(appointment => appointment.User)
                .WithMany(user => user.Appointments)
                .HasForeignKey(appointment => appointment.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(appointment => appointment.Service)
                .WithMany(service => service.Appointments)
                .HasForeignKey(appointment => appointment.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(appointment => appointment.Barber)
                .WithMany(barber => barber.Appointments)
                .HasForeignKey(appointment => appointment.BarberId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
