using System.Collections.Generic;
using System.Reflection.Emit;
using CocherasAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CocherasAPI.Services
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Cochera> Cocheras { get; set; }
        public DbSet<Distrito> Distritos { get; set; }
        public DbSet<Reserva> Reservas { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<DuenoCochera> DuenosCocheras { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure cascade delete behavior
            modelBuilder.Entity<Reserva>()
                .HasOne(r => r.Usuario)
                .WithMany(u => u.Reservas)
                .HasForeignKey(r => r.IdUsuario)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reserva>()
                .HasOne(r => r.Cochera)
                .WithMany(c => c.Reservas)
                .HasForeignKey(r => r.IdCochera)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Pago>()
                .HasOne(p => p.Reserva)
                .WithMany(r => r.Pagos)
                .HasForeignKey(p => p.IdReserva)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Cochera)
                .WithMany(c => c.Reviews)
                .HasForeignKey(r => r.IdCochera)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Usuario)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.IdUsuario)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DuenoCochera>()
                .HasOne(dc => dc.Usuario)
                .WithMany()
                .HasForeignKey(dc => dc.IdUsuario)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DuenoCochera>()
                .HasOne(dc => dc.Cochera)
                .WithMany()
                .HasForeignKey(dc => dc.IdCochera)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure unique constraints
            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email)
                .IsUnique();
        }
    }
}