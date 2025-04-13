using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using CocherasAPI.Data;
using CocherasAPI.Models;

namespace CocherasAPI.Services
{
    public interface IUsuarioService
    {
        Task<IEnumerable<Usuario>> GetAllUsuariosAsync();
        Task<Usuario> GetUsuarioByIdAsync(int id);
        Task<Usuario> GetUsuarioByEmailAsync(string email);
        Task<Usuario> CreateUsuarioAsync(Usuario usuario);
        Task<Usuario> UpdateUsuarioAsync(Usuario usuario);
        Task<bool> DeleteUsuarioAsync(int id);
        Task<bool> ValidateCredentialsAsync(string email, string password);
        Task<Usuario> AuthenticateAsync(string email, string password);
        Task<bool> EmailExistsAsync(string email);
        Task<bool> UpdatePasswordAsync(int userId, string currentPassword, string newPassword);
        Task<DateTime> UpdateLastLoginAsync(int userId);
    }

    public class UsuarioService : IUsuarioService
    {
        private readonly ApplicationDbContext _context;

        public UsuarioService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Usuario>> GetAllUsuariosAsync()
        {
            return await _context.Usuarios.ToListAsync();
        }

        public async Task<Usuario> GetUsuarioByIdAsync(int id)
        {
            return await _context.Usuarios.FindAsync(id);
        }

        public async Task<Usuario> GetUsuarioByEmailAsync(string email)
        {
            return await _context.Usuarios.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<Usuario> CreateUsuarioAsync(Usuario usuario)
        {
            // Comprobar si el email ya existe
            if (await EmailExistsAsync(usuario.Email))
            {
                throw new InvalidOperationException("El email ya está registrado");
            }

            // Hashear la contraseña
            usuario.Contrasena = HashPassword(usuario.Contrasena);

            // Establecer valores por defecto
            usuario.FechaRegistro = DateTime.Now;
            usuario.FechaActualizacion = DateTime.Now;
            usuario.Estado = "activo";

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return usuario;
        }

        public async Task<Usuario> UpdateUsuarioAsync(Usuario usuario)
        {
            var existingUsuario = await _context.Usuarios.FindAsync(usuario.IdUsuario);

            if (existingUsuario == null)
            {
                throw new KeyNotFoundException($"Usuario con ID {usuario.IdUsuario} no encontrado");
            }

            // Actualizar solo los campos permitidos
            existingUsuario.Nombre = usuario.Nombre;
            existingUsuario.Apellido = usuario.Apellido;
            existingUsuario.Telefono = usuario.Telefono;
            existingUsuario.Estado = usuario.Estado;
            existingUsuario.FechaActualizacion = DateTime.Now;

            // No actualizar email ni contraseña aquí

            _context.Usuarios.Update(existingUsuario);
            await _context.SaveChangesAsync();

            return existingUsuario;
        }

        public async Task<bool> DeleteUsuarioAsync(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);

            if (usuario == null)
            {
                return false;
            }

            // En lugar de eliminar, marcar como inactivo
            usuario.Estado = "inactivo";
            usuario.FechaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ValidateCredentialsAsync(string email, string password)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u =>
                u.Email.ToLower() == email.ToLower() && u.Estado == "activo");

            if (usuario == null)
            {
                return false;
            }

            return VerifyPassword(password, usuario.Contrasena);
        }

        public async Task<Usuario> AuthenticateAsync(string email, string password)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u =>
                u.Email.ToLower() == email.ToLower() && u.Estado == "activo");

            if (usuario == null || !VerifyPassword(password, usuario.Contrasena))
            {
                return null;
            }

            // Actualizar última conexión
            usuario.UltimaConexion = DateTime.Now;
            await _context.SaveChangesAsync();

            return usuario;
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.Usuarios.AnyAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<bool> UpdatePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var usuario = await _context.Usuarios.FindAsync(userId);

            if (usuario == null || !VerifyPassword(currentPassword, usuario.Contrasena))
            {
                return false;
            }

            usuario.Contrasena = HashPassword(newPassword);
            usuario.FechaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<DateTime> UpdateLastLoginAsync(int userId)
        {
            var usuario = await _context.Usuarios.FindAsync(userId);

            if (usuario == null)
            {
                throw new KeyNotFoundException($"Usuario con ID {userId} no encontrado");
            }

            usuario.UltimaConexion = DateTime.Now;
            await _context.SaveChangesAsync();

            return usuario.UltimaConexion.Value;
        }

        // Métodos auxiliares para hash de contraseñas
        private string HashPassword(string password)
        {
            // En una aplicación real, se debería usar un algoritmo más robusto como BCrypt
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private bool VerifyPassword(string password, string hashedPassword)
        {
            // En una aplicación real, se debería usar un algoritmo más robusto como BCrypt
            var hashedInput = HashPassword(password);
            return hashedInput == hashedPassword;
        }
    }
}