using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CocherasAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CocherasAPI.Services
{
    public interface ICocheraService
    {
        Task<IEnumerable<Cochera>> GetAllCocherasAsync();
        Task<Cochera> GetCocheraByIdAsync(int id);
        Task<IEnumerable<Cochera>> GetCocherasByDistritoAsync(int distritoId);
        Task<IEnumerable<Cochera>> GetCocherasByDuenoAsync(int duenoId);
        Task<IEnumerable<Cochera>> SearchCocherasAsync(int? distritoId, decimal? maxPrecio, TimeSpan? horaInicio, TimeSpan? horaFin, DateTime? fecha);
        Task<Cochera> CreateCocheraAsync(Cochera cochera);
        Task<Cochera> UpdateCocheraAsync(Cochera cochera);
        Task<bool> DeleteCocheraAsync(int id);
        Task<bool> UpdateDisponibilidadAsync(int id, bool disponible);
        Task<decimal> GetPromedioCalificacionAsync(int id);
    }

    public class CocheraService : ICocheraService
    {
        private readonly ApplicationDbContext _context;

        public CocheraService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Cochera>> GetAllCocherasAsync()
        {
            return await _context.Cocheras
                .Where(c => c.Disponible)
                .Include(c => c.Distrito)
                .Include(c => c.Dueno)
                .ToListAsync();
        }

        public async Task<Cochera> GetCocheraByIdAsync(int id)
        {
            return await _context.Cocheras
                .Include(c => c.Distrito)
                .Include(c => c.Dueno)
                .Include(c => c.Reviews)
                .FirstOrDefaultAsync(c => c.IdCochera == id);
        }

        public async Task<IEnumerable<Cochera>> GetCocherasByDistritoAsync(int distritoId)
        {
            return await _context.Cocheras
                .Where(c => c.IdDistrito == distritoId && c.Disponible)
                .Include(c => c.Distrito)
                .Include(c => c.Dueno)
                .ToListAsync();
        }

        public async Task<IEnumerable<Cochera>> GetCocherasByDuenoAsync(int duenoId)
        {
            return await _context.Cocheras
                .Where(c => c.IdDueno == duenoId)
                .Include(c => c.Distrito)
                .ToListAsync();
        }

        public async Task<IEnumerable<Cochera>> SearchCocherasAsync(int? distritoId, decimal? maxPrecio, TimeSpan? horaInicio, TimeSpan? horaFin, DateTime? fecha)
        {
            var query = _context.Cocheras
                .Where(c => c.Disponible);

            if (distritoId.HasValue)
            {
                query = query.Where(c => c.IdDistrito == distritoId.Value);
            }

            if (maxPrecio.HasValue)
            {
                query = query.Where(c => c.PrecioHora <= maxPrecio.Value);
            }

            if (horaInicio.HasValue)
            {
                query = query.Where(c => c.HoraApertura <= horaInicio.Value);
            }

            if (horaFin.HasValue)
            {
                query = query.Where(c => c.HoraCierre >= horaFin.Value);
            }

            // Check availability for specific date if provided
            if (fecha.HasValue)
            {
                var fechaInicio = fecha.Value.Date;
                var fechaFin = fechaInicio.AddDays(1);

                query = query
                    .Where(c => !c.Reservas.Any(r =>
                        r.Estado != "cancelada" &&
                        ((r.FechaInicio <= fechaInicio && r.FechaFin >= fechaInicio) ||
                         (r.FechaInicio <= fechaFin && r.FechaFin >= fechaFin) ||
                         (r.FechaInicio >= fechaInicio && r.FechaFin <= fechaFin))));
            }

            return await query
                .Include(c => c.Distrito)
                .Include(c => c.Dueno)
                .ToListAsync();
        }

        public async Task<Cochera> CreateCocheraAsync(Cochera cochera)
        {
            cochera.FechaRegistro = DateTime.Now;
            cochera.FechaActualizacion = DateTime.Now;

            _context.Cocheras.Add(cochera);
            await _context.SaveChangesAsync();
            return cochera;
        }

        public async Task<Cochera> UpdateCocheraAsync(Cochera cochera)
        {
            var existingCochera = await _context.Cocheras.FindAsync(cochera.IdCochera);

            if (existingCochera == null)
                return null;

            existingCochera.IdDistrito = cochera.IdDistrito;
            existingCochera.Direccion = cochera.Direccion;
            existingCochera.Capacidad = cochera.Capacidad;
            existingCochera.PrecioHora = cochera.PrecioHora;
            existingCochera.Disponible = cochera.Disponible;
            existingCochera.Descripcion = cochera.Descripcion;
            existingCochera.HoraApertura = cochera.HoraApertura;
            existingCochera.HoraCierre = cochera.HoraCierre;
            existingCochera.FechaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();
            return existingCochera;
        }

        public async Task<bool> DeleteCocheraAsync(int id)
        {
            var cochera = await _context.Cocheras.FindAsync(id);
            if (cochera == null)
                return false;

            // Check if there are active reservations
            var hasActiveReservations = await _context.Reservas
                .AnyAsync(r => r.IdCochera == id &&
                          (r.Estado == "pendiente" || r.Estado == "confirmada") &&
                          r.FechaFin >= DateTime.Now);

            if (hasActiveReservations)
                return false;

            _context.Cocheras.Remove(cochera);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateDisponibilidadAsync(int id, bool disponible)
        {
            var cochera = await _context.Cocheras.FindAsync(id);
            if (cochera == null)
                return false;

            cochera.Disponible = disponible;
            cochera.FechaActualizacion = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<decimal> GetPromedioCalificacionAsync(int id)
        {
            var reviews = await _context.Reviews
                .Where(r => r.IdCochera == id)
                .ToListAsync();

            if (!reviews.Any())
                return 0;

            return (decimal)reviews.Average(r => r.Calificacion);
        }
    }
}