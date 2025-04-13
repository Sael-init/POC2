using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CocherasAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CocherasAPI.Services
{
    public interface IDuenoCocheraService
    {
        Task<IEnumerable<DuenoCochera>> GetAllDuenosCocherasAsync();
        Task<DuenoCochera> GetDuenoCocheraByIdAsync(int id);
        Task<IEnumerable<DuenoCochera>> GetDuenosCocherasByUsuarioAsync(int usuarioId);
        Task<IEnumerable<DuenoCochera>> GetDuenosCocherasByCocheraAsync(int cocheraId);
        Task<DuenoCochera> AssignDuenoToCocheraAsync(int usuarioId, int cocheraId);
        Task<bool> RemoveDuenoFromCocheraAsync(int id);
    }

    public class DuenoCocheraService : IDuenoCocheraService
    {
        private readonly ApplicationDbContext _context;

        public DuenoCocheraService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<DuenoCochera>> GetAllDuenosCocherasAsync()
        {
            return await _context.DuenosCocheras
                .Include(dc => dc.Usuario)
                .Include(dc => dc.Cochera)
                .ToListAsync();
        }

        public async Task<DuenoCochera> GetDuenoCocheraByIdAsync(int id)
        {
            return await _context.DuenosCocheras
                .Include(dc => dc.Usuario)
                .Include(dc => dc.Cochera)
                .FirstOrDefaultAsync(dc => dc.Id == id);
        }

        public async Task<IEnumerable<DuenoCochera>> GetDuenosCocherasByUsuarioAsync(int usuarioId)
        {
            return await _context.DuenosCocheras
                .Where(dc => dc.IdUsuario == usuarioId)
                .Include(dc => dc.Cochera)
                .ToListAsync();
        }

        public async Task<IEnumerable<DuenoCochera>> GetDuenosCocherasByCocheraAsync(int cocheraId)
        {
            return await _context.DuenosCocheras
                .Where(dc => dc.IdCochera == cocheraId)
                .Include(dc => dc.Usuario)
                .ToListAsync();
        }

        public async Task<DuenoCochera> AssignDuenoToCocheraAsync(int usuarioId, int cocheraId)
        {
            // Check if user exists
            var user = await _context.Usuarios.FindAsync(usuarioId);
            if (user == null)
                throw new KeyNotFoundException($"Usuario con ID {usuarioId} no encontrado");

            // Check if cochera exists
            var cochera = await _context.Cocheras.FindAsync(cocheraId);
            if (cochera == null)
                throw new KeyNotFoundException($"Cochera con ID {cocheraId} no encontrada");

            // Check if assignment already exists
            var existingAssignment = await _context.DuenosCocheras
                .FirstOrDefaultAsync(dc => dc.IdUsuario == usuarioId && dc.IdCochera == cocheraId);

            if (existingAssignment != null)
                return existingAssignment;

            // Create new assignment
            var duenoCochera = new DuenoCochera
            {
                IdUsuario = usuarioId,
                IdCochera = cocheraId,
                FechaAsignacion = DateTime.Now
            };

            _context.DuenosCocheras.Add(duenoCochera);

            // Also update the Cochera owner if it's not set
            if (cochera.IdDueno == 0)
            {
                cochera.IdDueno = usuarioId;
                cochera.FechaActualizacion = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return duenoCochera;
        }

        public async Task<bool> RemoveDuenoFromCocheraAsync(int id)
        {
            var duenoCochera = await _context.DuenosCocheras.FindAsync(id);
            if (duenoCochera == null)
                return false;

            // Check if there are active reservations
            var hasActiveReservations = await _context.Reservas
                .AnyAsync(r => r.IdCochera == duenoCochera.IdCochera &&
                          (r.Estado == "pendiente" || r.Estado == "confirmada") &&
                          r.FechaFin >= DateTime.Now);

            if (hasActiveReservations)
                return false;

            _context.DuenosCocheras.Remove(duenoCochera);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}