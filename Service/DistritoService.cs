using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CocherasAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CocherasAPI.Services
{
    public interface IDistritoService
    {
        Task<IEnumerable<Distrito>> GetAllDistritosAsync();
        Task<Distrito> GetDistritoByIdAsync(int id);
        Task<Distrito> CreateDistritoAsync(Distrito distrito);
        Task<Distrito> UpdateDistritoAsync(Distrito distrito);
        Task<bool> DeleteDistritoAsync(int id);
    }

    public class DistritoService : IDistritoService
    {
        private readonly ApplicationDbContext _context;

        public DistritoService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Distrito>> GetAllDistritosAsync()
        {
            return await _context.Distritos.ToListAsync();
        }

        public async Task<Distrito> GetDistritoByIdAsync(int id)
        {
            return await _context.Distritos.FindAsync(id);
        }

        public async Task<Distrito> CreateDistritoAsync(Distrito distrito)
        {
            _context.Distritos.Add(distrito);
            await _context.SaveChangesAsync();
            return distrito;
        }

        public async Task<Distrito> UpdateDistritoAsync(Distrito distrito)
        {
            var existingDistrito = await _context.Distritos.FindAsync(distrito.IdDistrito);

            if (existingDistrito == null)
                return null;

            // Update properties here
            // Note: You'll need to update these based on your actual Distrito model properties
            // This is just a placeholder since the provided Distrito.cs was empty

            await _context.SaveChangesAsync();
            return existingDistrito;
        }

        public async Task<bool> DeleteDistritoAsync(int id)
        {
            var distrito = await _context.Distritos.FindAsync(id);
            if (distrito == null)
                return false;

            // Check if there are cocheras in this distrito
            var hasCocheras = await _context.Cocheras.AnyAsync(c => c.IdDistrito == id);
            if (hasCocheras)
                return false;

            _context.Distritos.Remove(distrito);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}