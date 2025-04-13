using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CocherasAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CocherasAPI.Services
{
    public interface IReviewService
    {
        Task<IEnumerable<Review>> GetAllReviewsAsync();
        Task<Review> GetReviewByIdAsync(int id);
        Task<IEnumerable<Review>> GetReviewsByCocheraAsync(int cocheraId);
        Task<IEnumerable<Review>> GetReviewsByUsuarioAsync(int usuarioId);
        Task<Review> GetReviewByReservaAsync(int reservaId);
        Task<bool> CanUserReviewAsync(int usuarioId, int cocheraId);
        Task<Review> CreateReviewAsync(Review review);
        Task<Review> UpdateReviewAsync(Review review);
        Task<bool> DeleteReviewAsync(int id);
    }

    public class ReviewService : IReviewService
    {
        private readonly ApplicationDbContext _context;

        public ReviewService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Review>> GetAllReviewsAsync()
        {
            return await _context.Reviews
                .Include(r => r.Usuario)
                .Include(r => r.Cochera)
                .ToListAsync();
        }

        public async Task<Review> GetReviewByIdAsync(int id)
        {
            return await _context.Reviews
                .Include(r => r.Usuario)
                .Include(r => r.Cochera)
                .FirstOrDefaultAsync(r => r.IdReview == id);
        }

        public async Task<IEnumerable<Review>> GetReviewsByCocheraAsync(int cocheraId)
        {
            return await _context.Reviews
                .Where(r => r.IdCochera == cocheraId)
                .Include(r => r.Usuario)
                .OrderByDescending(r => r.FechaReview)
                .ToListAsync();
        }

        public async Task<IEnumerable<Review>> GetReviewsByUsuarioAsync(int usuarioId)
        {
            return await _context.Reviews
                .Where(r => r.IdUsuario == usuarioId)
                .Include(r => r.Cochera)
                .OrderByDescending(r => r.FechaReview)
                .ToListAsync();
        }

        public async Task<Review> GetReviewByReservaAsync(int reservaId)
        {
            return await _context.Reviews
                .Include(r => r.Usuario)
                .Include(r => r.Cochera)
                .FirstOrDefaultAsync(r => r.IdReserva == reservaId);
        }

        public async Task<bool> CanUserReviewAsync(int usuarioId, int cocheraId)
        {
            // Check if the user has completed a reservation for this cochera
            var hasCompletedReservation = await _context.Reservas
                .AnyAsync(r => r.IdUsuario == usuarioId &&
                          r.IdCochera == cocheraId &&
                          r.Estado == "completada");

            if (!hasCompletedReservation)
                return false;

            // Check if the user has already reviewed this cochera
            var hasExistingReview = await _context.Reviews
                .AnyAsync(r => r.IdUsuario == usuarioId && r.IdCochera == cocheraId);

            return !hasExistingReview;
        }

        public async Task<Review> CreateReviewAsync(Review review)
        {
            // Validate if the user can review
            var canReview = await CanUserReviewAsync(review.IdUsuario, review.IdCochera);
            if (!canReview && review.IdReserva == null)
                throw new InvalidOperationException("El usuario no puede revisar esta cochera");

            // If a reservation ID is provided, validate it
            if (review.IdReserva.HasValue)
            {
                var reserva = await _context.Reservas.FindAsync(review.IdReserva.Value);
                if (reserva == null || reserva.Estado != "completada" ||
                    reserva.IdUsuario != review.IdUsuario ||
                    reserva.IdCochera != review.IdCochera)
                {
                    throw new InvalidOperationException("La reserva no es válida para esta revisión");
                }
            }

            review.FechaReview = DateTime.Now;
            review.FechaActualizacion = DateTime.Now;

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();
            return review;
        }

        public async Task<Review> UpdateReviewAsync(Review review)
        {
            var existingReview = await _context.Reviews.FindAsync(review.IdReview);

            if (existingReview == null)
                return null;

            // Only allow the original user to update their review
            if (existingReview.IdUsuario != review.IdUsuario)
                throw new InvalidOperationException("No tiene permiso para modificar esta revisión");

            existingReview.Calificacion = review.Calificacion;
            existingReview.Comentario = review.Comentario;
            existingReview.FechaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();
            return existingReview;
        }

        public async Task<bool> DeleteReviewAsync(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
                return false;

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}