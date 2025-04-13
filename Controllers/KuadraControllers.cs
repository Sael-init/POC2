using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using CocherasAPI.Models;
using CocherasAPI.Data;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CocherasAPI.Controllers
{
    // Controller para autenticación (login y register)
    [Route("api")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // POST: api/register
        [HttpPost("register")]
        public async Task<ActionResult<object>> Register(Usuario usuario)
        {
            // Comprobar si el email ya existe
            if (await _context.Usuarios.AnyAsync(u => u.Email == usuario.Email))
            {
                return BadRequest(new { message = "El email ya está registrado" });
            }

            // En una aplicación real, se debería hashear la contraseña
            usuario.FechaRegistro = DateTime.Now;
            usuario.FechaActualizacion = DateTime.Now;
            usuario.Estado = "activo";

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            // Generar token JWT para login automático
            var token = GenerarJwtToken(usuario);

            return Ok(new
            {
                userId = usuario.IdUsuario,
                token,
                message = "Usuario registrado correctamente"
            });
        }

        // PATCH: api/login
        [HttpPatch("login")]
        public async Task<ActionResult<object>> Login(LoginModel model)
        {
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == model.Email);

            if (usuario == null)
            {
                return NotFound(new { message = "Usuario no encontrado" });
            }

            // En una aplicación real, se debería verificar el hash de la contraseña
            if (usuario.Contrasena != model.Password)
            {
                return Unauthorized(new { message = "Credenciales incorrectas" });
            }

            // Actualizar última conexión
            usuario.UltimaConexion = DateTime.Now;
            await _context.SaveChangesAsync();

            // Generar token JWT
            var token = GenerarJwtToken(usuario);

            return Ok(new
            {
                userId = usuario.IdUsuario,
                token,
                nombre = usuario.Nombre,
                apellido = usuario.Apellido,
                email = usuario.Email
            });
        }

        private string GenerarJwtToken(Usuario usuario)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.Now.AddDays(1);

            var token = new JwtSecurityToken(
                _configuration["JwtIssuer"],
                _configuration["JwtAudience"],
                claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    // Controller para búsqueda de cocheras
    [Route("api")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SearchController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/search
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Cochera>>> Search([FromQuery] SearchParameters parameters)
        {
            // Iniciar con todas las cocheras disponibles
            var query = _context.Cocheras
                .Where(c => c.Disponible)
                .Include(c => c.Distrito)
                .Include(c => c.Dueno)
                .AsQueryable();

            // Filtrar por distrito si se proporciona
            if (parameters.IdDistrito.HasValue)
            {
                query = query.Where(c => c.IdDistrito == parameters.IdDistrito.Value);
            }

            // Filtrar por precio máximo si se proporciona
            if (parameters.PrecioMaximo.HasValue)
            {
                query = query.Where(c => c.PrecioHora <= parameters.PrecioMaximo.Value);
            }

            // Filtrar por precio mínimo si se proporciona
            if (parameters.PrecioMinimo.HasValue)
            {
                query = query.Where(c => c.PrecioHora >= parameters.PrecioMinimo.Value);
            }

            // Filtrar por capacidad si se proporciona
            if (parameters.Capacidad.HasValue)
            {
                query = query.Where(c => c.Capacidad >= parameters.Capacidad.Value);
            }

            // Filtrar por disponibilidad en fechas específicas si se proporcionan
            if (parameters.FechaInicio.HasValue && parameters.FechaFin.HasValue)
            {
                DateTime fechaInicio = parameters.FechaInicio.Value;
                DateTime fechaFin = parameters.FechaFin.Value;

                // Excluir cocheras que ya tienen reservas en ese período
                query = query.Where(c => !c.Reservas.Any(r =>
                    r.Estado != "cancelada" &&
                    ((fechaInicio <= r.FechaFin && fechaFin >= r.FechaInicio))));
            }

            // Ordenar según el parámetro
            switch (parameters.OrdenarPor?.ToLower())
            {
                case "precio_asc":
                    query = query.OrderBy(c => c.PrecioHora);
                    break;
                case "precio_desc":
                    query = query.OrderByDescending(c => c.PrecioHora);
                    break;
                case "calificacion":
                    query = query.OrderByDescending(c => c.Reviews.Average(r => r.Calificacion));
                    break;
                default:
                    // Por defecto ordenar por precio ascendente
                    query = query.OrderBy(c => c.PrecioHora);
                    break;
            }

            // Paginación
            int pagina = parameters.Pagina ?? 1;
            int porPagina = parameters.PorPagina ?? 10;

            var total = await query.CountAsync();
            var resultados = await query
                .Skip((pagina - 1) * porPagina)
                .Take(porPagina)
                .ToListAsync();

            // Configurar cabeceras de paginación
            Response.Headers.Add("X-Total-Count", total.ToString());
            Response.Headers.Add("X-Total-Pages", ((int)Math.Ceiling((double)total / porPagina)).ToString());

            return Ok(resultados);
        }
    }

    // Controller para gestión de cocheras
    [Route("api/manageCocheras")]
    [ApiController]
    [Authorize] // Requiere autenticación
    public class CocheraManagementController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CocheraManagementController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/manageCocheras
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cochera>>> GetCocheras()
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            // Devolver solo las cocheras que pertenecen al usuario actual
            return await _context.Cocheras
                .Where(c => c.IdDueno == userId)
                .Include(c => c.Distrito)
                .ToListAsync();
        }

        // POST: api/manageCocheras
        [HttpPost]
        public async Task<ActionResult<Cochera>> CreateCochera(Cochera cochera)
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            // Asignar al usuario actual como dueño
            cochera.IdDueno = userId;
            cochera.FechaRegistro = DateTime.Now;
            cochera.FechaActualizacion = DateTime.Now;

            _context.Cocheras.Add(cochera);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCocheras), new { id = cochera.IdCochera }, cochera);
        }

        // PATCH: api/manageCocheras/{id}
        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateCochera(int id, CocheraUpdateModel model)
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            var cochera = await _context.Cocheras.FindAsync(id);
            if (cochera == null)
            {
                return NotFound();
            }

            // Verificar que el usuario actual es el dueño
            if (cochera.IdDueno != userId)
            {
                return Forbid();
            }

            // Actualizar solo las propiedades proporcionadas
            if (model.Direccion != null)
                cochera.Direccion = model.Direccion;

            if (model.PrecioHora.HasValue)
                cochera.PrecioHora = model.PrecioHora.Value;

            if (model.Disponible.HasValue)
                cochera.Disponible = model.Disponible.Value;

            if (model.Descripcion != null)
                cochera.Descripcion = model.Descripcion;

            if (model.HoraApertura.HasValue)
                cochera.HoraApertura = model.HoraApertura.Value;

            if (model.HoraCierre.HasValue)
                cochera.HoraCierre = model.HoraCierre.Value;

            if (model.IdDistrito.HasValue)
                cochera.IdDistrito = model.IdDistrito.Value;

            if (model.Capacidad.HasValue)
                cochera.Capacidad = model.Capacidad.Value;

            cochera.FechaActualizacion = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Cocheras.Any(e => e.IdCochera == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/manageCocheras/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCochera(int id)
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            var cochera = await _context.Cocheras.FindAsync(id);
            if (cochera == null)
            {
                return NotFound();
            }

            // Verificar que el usuario actual es el dueño
            if (cochera.IdDueno != userId)
            {
                return Forbid();
            }

            _context.Cocheras.Remove(cochera);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    // Controller para gestión de reservas
    [Route("api/manageReserva")]
    [ApiController]
    [Authorize] // Requiere autenticación
    public class ReservaManagementController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReservaManagementController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/manageReserva
        [HttpPost]
        public async Task<ActionResult<Reserva>> CreateReserva(Reserva reserva)
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            // Asignar al usuario actual
            reserva.IdUsuario = userId;

            // Verificar que la cochera existe
            var cochera = await _context.Cocheras.FindAsync(reserva.IdCochera);
            if (cochera == null)
            {
                return BadRequest("La cochera no existe");
            }

            // Verificar que la cochera está disponible
            if (!cochera.Disponible)
            {
                return BadRequest("La cochera no está disponible");
            }

            // Verificar que no hay reservas solapadas
            bool hayReservasSolapadas = await _context.Reservas
                .Where(r => r.IdCochera == reserva.IdCochera)
                .Where(r => r.Estado != "cancelada")
                .Where(r => (reserva.FechaInicio <= r.FechaFin && reserva.FechaFin >= r.FechaInicio))
                .AnyAsync();

            if (hayReservasSolapadas)
            {
                return BadRequest("La cochera ya está reservada en el período seleccionado");
            }

            reserva.CreadaEn = DateTime.Now;
            reserva.ActualizadaEn = DateTime.Now;
            reserva.Estado = "pendiente";

            _context.Reservas.Add(reserva);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetReserva), new { id = reserva.IdReserva }, reserva);
        }

        // Método auxiliar para GetReserva (no es un endpoint)
        private async Task<ActionResult<Reserva>> GetReserva(int id)
        {
            var reserva = await _context.Reservas
                .Include(r => r.Cochera)
                .FirstOrDefaultAsync(r => r.IdReserva == id);

            if (reserva == null)
            {
                return NotFound();
            }

            return reserva;
        }

        // PATCH: api/manageReserva/{id}
        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateReserva(int id, ReservaUpdateModel model)
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva == null)
            {
                return NotFound();
            }

            // Verificar que el usuario actual es el propietario de la reserva o el dueño de la cochera
            var cochera = await _context.Cocheras.FindAsync(reserva.IdCochera);
            if (reserva.IdUsuario != userId && cochera?.IdDueno != userId)
            {
                return Forbid();
            }

            // Actualizar el estado si se proporciona
            if (model.Estado != null)
            {
                // Validar que el estado sea válido
                string[] estadosValidos = { "pendiente", "confirmada", "cancelada", "completada" };
                if (!estadosValidos.Contains(model.Estado))
                {
                    return BadRequest("Estado de reserva no válido");
                }

                reserva.Estado = model.Estado;
            }

            // Actualizar fechas si se proporcionan (solo permitido si el estado no es "completada" o "cancelada")
            if (reserva.Estado != "completada" && reserva.Estado != "cancelada")
            {
                if (model.FechaInicio.HasValue)
                {
                    // Verificar que no hay reservas solapadas con la nueva fecha
                    if (model.FechaFin.HasValue && model.FechaInicio.HasValue)
                    {
                        bool hayReservasSolapadas = await _context.Reservas
                            .Where(r => r.IdCochera == reserva.IdCochera)
                            .Where(r => r.IdReserva != id)
                            .Where(r => r.Estado != "cancelada")
                            .Where(r => (model.FechaInicio.Value <= r.FechaFin && model.FechaFin.Value >= r.FechaInicio))
                            .AnyAsync();

                        if (hayReservasSolapadas)
                        {
                            return BadRequest("La cochera ya está reservada en el nuevo período seleccionado");
                        }

                        reserva.FechaInicio = model.FechaInicio.Value;
                        reserva.FechaFin = model.FechaFin.Value;
                    }
                }
            }

            reserva.ActualizadaEn = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Reservas.Any(e => e.IdReserva == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/manageReserva/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReserva(int id)
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva == null)
            {
                return NotFound();
            }

            // Verificar que el usuario actual es el propietario de la reserva
            if (reserva.IdUsuario != userId)
            {
                return Forbid();
            }

            // En lugar de eliminar, marcar como cancelada si la reserva no ha comenzado
            if (reserva.FechaInicio > DateTime.Now)
            {
                reserva.Estado = "cancelada";
                reserva.ActualizadaEn = DateTime.Now;
                await _context.SaveChangesAsync();
                return NoContent();
            }

            // Si la reserva ya ha comenzado, no se permite eliminarla
            return BadRequest("No se puede eliminar una reserva que ya ha comenzado");
        }
    }

    // Controller para pagos
    [Route("api/payment")]
    [ApiController]
    [Authorize] // Requiere autenticación
    public class PaymentController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PaymentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/payment
        [HttpPost]
        public async Task<ActionResult<Pago>> CreatePayment(Pago pago)
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            // Asignar al usuario actual
            pago.IdUsuario = userId;

            // Verificar que la reserva existe
            var reserva = await _context.Reservas.FindAsync(pago.IdReserva);
            if (reserva == null)
            {
                return BadRequest("La reserva no existe");
            }

            // Verificar que el usuario actual es el propietario de la reserva
            if (reserva.IdUsuario != userId)
            {
                return Forbid();
            }

            // Verificar que la reserva está en estado pendiente o confirmada
            if (reserva.Estado != "pendiente" && reserva.Estado != "confirmada")
            {
                return BadRequest("La reserva no está en un estado válido para el pago");
            }

            pago.FechaCreacion = DateTime.Now;
            pago.FechaActualizacion = DateTime.Now;
            pago.FechaPago = DateTime.Now;
            pago.Estado = "completado"; // En un caso real, esto dependería del procesamiento del pago

            _context.Pagos.Add(pago);

            // Actualizar el estado de la reserva a "confirmada"
            reserva.Estado = "confirmada";
            reserva.ActualizadaEn = DateTime.Now;

            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPayment), new { id = pago.IdPago }, pago);
        }

        // Método auxiliar para GetPayment (no es un endpoint)
        private async Task<ActionResult<Pago>> GetPayment(int id)
        {
            var pago = await _context.Pagos
                .Include(p => p.Reserva)
                .FirstOrDefaultAsync(p => p.IdPago == id);

            if (pago == null)
            {
                return NotFound();
            }

            return pago;
        }
    }

    // Controller para geolocalización
    [Route("api/geolocalization")]
    [ApiController]
    public class GeolocalizationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public GeolocalizationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/geolocalization
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CocheraGeoModel>>> GetCocherasGeo([FromQuery] GeoParameters parameters)
        {
            if (!parameters.Latitud.HasValue || !parameters.Longitud.HasValue)
            {
                return BadRequest("Se requieren latitud y longitud");
            }

            // En una aplicación real, aquí se realizaría el cálculo de distancia usando las coordenadas
            // Para este ejemplo, simulamos devolver cocheras cercanas al punto especificado

            var cocheras = await _context.Cocheras
                .Where(c => c.Disponible)
                .Include(c => c.Distrito)
                .Take(10) // Limitar a 10 resultados para el ejemplo
                .Select(c => new CocheraGeoModel
                {
                    IdCochera = c.IdCochera,
                    Direccion = c.Direccion,
                    DistritoNombre = c.Distrito.Nombre,
                    PrecioHora = c.PrecioHora,
                    // Simulamos coordenadas cercanas a las proporcionadas (±0.01 grados)
                    Latitud = parameters.Latitud.Value + (new Random().NextDouble() * 0.02 - 0.01),
                    Longitud = parameters.Longitud.Value + (new Random().NextDouble() * 0.02 - 0.01),
                    // Simulamos una distancia calculada (entre 100m y 2km)
                    DistanciaEnMetros = new Random().Next(100, 2000)
                })
                .OrderBy(c => c.DistanciaEnMetros)
                .ToListAsync();

            return Ok(cocheras);
        }
    }

    // Controller para reseñas
    [Route("api/review")]
    [ApiController]
    public class ReviewController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/review
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Review>>> GetReviews([FromQuery] int? idCochera, [FromQuery] int? idUsuario)
        {
            var query = _context.Reviews
                .Include(r => r.Usuario)
                .Include(r => r.Cochera)
                .AsQueryable();

            if (idCochera.HasValue)
            {
                query = query.Where(r => r.IdCochera == idCochera.Value);
            }

            if (idUsuario.HasValue)
            {
                query = query.Where(r => r.IdUsuario == idUsuario.Value);
            }

            return await query.ToListAsync();
        }

        // POST: api/review
        [HttpPost]
        [Authorize] // Requiere autenticación
        public async Task<ActionResult<Review>> CreateReview(Review review)
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            // Asignar al usuario actual
            review.IdUsuario = userId;

            // Verificar que la cochera existe
            var cochera = await _context.Cocheras.FindAsync(review.IdCochera);
            if (cochera == null)
            {
                return BadRequest("La cochera no existe");
            }

            // Verificar que el usuario ha reservado esta cochera (opcional)
            if (review.IdReserva.HasValue)
            {
                var reserva = await _context.Reservas.FindAsync(review.IdReserva.Value);
                if (reserva == null || reserva.IdUsuario != userId || reserva.IdCochera != review.IdCochera)
                {
                    return BadRequest("La reserva no existe o no pertenece al usuario o no corresponde a la cochera");
                }

                // Verificar que la reserva está completada
                if (reserva.Estado != "completada")
                {
                    return BadRequest("Solo se pueden dejar reseñas para reservas completadas");
                }
            }
            else
            {
                // Si no hay ID de reserva, verificar que el usuario ha tenido alguna reserva completada en esta cochera
                bool haReservadoCochera = await _context.Reservas
                    .AnyAsync(r => r.IdUsuario == userId && r.IdCochera == review.IdCochera && r.Estado == "completada");

                if (!haReservadoCochera)
                {
                    return BadRequest("Solo se pueden dejar reseñas para cocheras que has usado");
                }
            }

            // Verificar que el usuario no ha dejado ya una reseña para esta cochera/reserva
            if (review.IdReserva.HasValue)
            {
                bool yaHayReseña = await _context.Reviews
                    .AnyAsync(r => r.IdUsuario == userId && r.IdReserva == review.IdReserva);

                if (yaHayReseña)
                {
                    return BadRequest("Ya has dejado una reseña para esta reserva");
                }
            }
            else
            {
                bool yaHayReseña = await _context.Reviews
                    .AnyAsync(r => r.IdUsuario == userId && r.IdCochera == review.IdCochera);

                if (yaHayReseña)
                {
                    return BadRequest("Ya has dejado una reseña para esta cochera");
                }
            }

            review.FechaReview = DateTime.Now;
            review.FechaActualizacion = DateTime.Now;

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetReviews), new { idCochera = review.IdCochera }, review);
        }

        // PATCH: api/review/{id}
        [HttpPatch("{id}")]
        [Authorize] // Requiere autenticación
        public async Task<IActionResult> UpdateReview(int id, ReviewUpdateModel model)
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            // Verificar que el usuario actual es el autor de la reseña
            if (review.IdUsuario != userId)
            {
                return Forbid();
            }

            // Actualizar solo las propiedades proporcionadas
            if (model.Calificacion.HasValue)
            {
                if (model.Calificacion.Value < 1 || model.Calificacion.Value > 5)
                {
                    return BadRequest("La calificación debe estar entre 1 y 5");
                }
                review.Calificacion = model.Calificacion.Value;
            }

            if (model.Comentario != null)
            {
                review.Comentario = model.Comentario;
            }

            review.FechaActualizacion = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Reviews.Any(e => e.IdReview == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/review/{id}
        [HttpDelete("{id}")]
        [Authorize] // Requiere autenticación
        public async Task<IActionResult> DeleteReview(int id)
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            // Verificar que el usuario actual es el autor de la reseña o el dueño de la cochera
            var esPropietario = review.IdUsuario == userId;

            // Verificar si es el dueño de la cochera
            var esDueno = false;
            var cochera = await _context.Cocheras.FindAsync(review.IdCochera);
            if (cochera != null && cochera.IdDueno == userId)
            {
                esDueno = true;
            }

            if (!esPropietario && !esDueno)
            {
                return Forbid("Solo el autor de la reseña o el dueño de la cochera pueden eliminar esta reseña");
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    // Modelos para las actualizaciones parciales y parámetros
    public class LoginModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }

    public class CocheraUpdateModel
    {
        [StringLength(255)]
        public string Direccion { get; set; }

        [Range(0.01, 999999.99)]
        public decimal? PrecioHora { get; set; }

        public bool? Disponible { get; set; }

        public string Descripcion { get; set; }

        public TimeSpan? HoraApertura { get; set; }

        public TimeSpan? HoraCierre { get; set; }

        public int? IdDistrito { get; set; }

        [Range(1, 100)]
        public int? Capacidad { get; set; }
    }

    public class ReservaUpdateModel
    {
        public DateTime? FechaInicio { get; set; }

        public DateTime? FechaFin { get; set; }

        [StringLength(20)]
        public string Estado { get; set; }
    }

    public class ReviewUpdateModel
    {
        [Range(1, 5)]
        public int? Calificacion { get; set; }

        public string Comentario { get; set; }
    }

    public class SearchParameters
    {
        public int? IdDistrito { get; set; }

        [Range(0, 999999.99)]
        public decimal? PrecioMinimo { get; set; }

        [Range(0, 999999.99)]
        public decimal? PrecioMaximo { get; set; }

        [Range(1, 100)]
        public int? Capacidad { get; set; }

        public DateTime? FechaInicio { get; set; }

        public DateTime? FechaFin { get; set; }

        public string OrdenarPor { get; set; }

        [Range(1, 100)]
        public int? Pagina { get; set; } = 1;

        [Range(1, 100)]
        public int? PorPagina { get; set; } = 10;
    }

    public class GeoParameters
    {
        [Required]
        public double? Latitud { get; set; }

        [Required]
        public double? Longitud { get; set; }

        [Range(0.1, 50.0)]
        public double? RadioEnKm { get; set; } = 2.0;
    }

    public class CocheraGeoModel
    {
        public int IdCochera { get; set; }
        public string Direccion { get; set; }
        public string DistritoNombre { get; set; }
        public decimal PrecioHora { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public int DistanciaEnMetros { get; set; }
    }

    // Endpoints adicionales para notificaciones
    [Route("api/notificaciones")]
    [ApiController]
    [Authorize]
    public class NotificacionesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public NotificacionesController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // GET: api/notificaciones
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Notificacion>>> GetNotificaciones()
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            var notificaciones = await _context.Notificaciones
                .Where(n => n.IdUsuario == userId)
                .OrderByDescending(n => n.FechaCreacion)
                .ToListAsync();

            return Ok(notificaciones);
        }

        // PATCH: api/notificaciones/{id}/marcar-leida
        [HttpPatch("{id}/marcar-leida")]
        public async Task<IActionResult> MarcarNotificacionLeida(int id)
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            var notificacion = await _context.Notificaciones.FindAsync(id);
            if (notificacion == null)
            {
                return NotFound();
            }

            // Verificar que la notificación pertenece al usuario actual
            if (notificacion.IdUsuario != userId)
            {
                return Forbid();
            }

            notificacion.Leida = true;
            notificacion.FechaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/notificaciones/marcar-todas-leidas
        [HttpPost("marcar-todas-leidas")]
        public async Task<IActionResult> MarcarTodasLeidas()
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            var notificaciones = await _context.Notificaciones
                .Where(n => n.IdUsuario == userId && !n.Leida)
                .ToListAsync();

            foreach (var notificacion in notificaciones)
            {
                notificacion.Leida = true;
                notificacion.FechaActualizacion = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/notificaciones/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotificacion(int id)
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            var notificacion = await _context.Notificaciones.FindAsync(id);
            if (notificacion == null)
            {
                return NotFound();
            }

            // Verificar que la notificación pertenece al usuario actual
            if (notificacion.IdUsuario != userId)
            {
                return Forbid();
            }

            _context.Notificaciones.Remove(notificacion);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    // Endpoints para procesamiento de pagos
    [Route("api/payment-process")]
    [ApiController]
    [Authorize]
    public class PaymentProcessController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public PaymentProcessController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // POST: api/payment-process/iniciar
        [HttpPost("iniciar")]
        public async Task<ActionResult<object>> IniciarPago(IniciarPagoModel model)
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            // Verificar que la reserva existe
            var reserva = await _context.Reservas
                .Include(r => r.Cochera)
                .FirstOrDefaultAsync(r => r.IdReserva == model.IdReserva);

            if (reserva == null)
            {
                return BadRequest("La reserva no existe");
            }

            // Verificar que el usuario actual es el propietario de la reserva
            if (reserva.IdUsuario != userId)
            {
                return Forbid();
            }

            // Verificar que la reserva está en estado pendiente o confirmada
            if (reserva.Estado != "pendiente" && reserva.Estado != "confirmada")
            {
                return BadRequest("La reserva no está en un estado válido para el pago");
            }

            // Calcular el monto basado en la duración de la reserva y el precio de la cochera
            var duracion = (reserva.FechaFin - reserva.FechaInicio).TotalHours;
            var monto = (decimal)duracion * reserva.Cochera.PrecioHora;

            // En una aplicación real, aquí se generaría una intent de pago con un proveedor como Stripe
            var paymentIntentId = $"pi_{Guid.NewGuid().ToString("N")}";

            // Guardar el intent de pago en el sistema
            var pago = new Pago
            {
                IdReserva = model.IdReserva,
                IdUsuario = userId,
                Monto = monto,
                MetodoPago = model.MetodoPago,
                ReferenciaPago = paymentIntentId,
                Estado = "pendiente",
                FechaCreacion = DateTime.Now,
                FechaActualizacion = DateTime.Now
            };

            _context.Pagos.Add(pago);
            await _context.SaveChangesAsync();

            // En una aplicación real, se retornaría la información necesaria para que el cliente complete el pago
            // Por ejemplo, un client_secret para Stripe
            return Ok(new
            {
                IdPago = pago.IdPago,
                Monto = monto,
                ReferenciaPago = paymentIntentId,
                ClientSecret = $"cs_{Guid.NewGuid().ToString("N")}",
                UrlPago = $"https://api.example.com/checkout/{paymentIntentId}"
            });
        }

        // POST: api/payment-process/confirmar
        [HttpPost("confirmar")]
        public async Task<ActionResult<object>> ConfirmarPago(ConfirmarPagoModel model)
        {
            // Obtener el ID del usuario actual desde el claim
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
            {
                return Unauthorized();
            }

            // Buscar el pago por su referencia
            var pago = await _context.Pagos
                .Include(p => p.Reserva)
                .FirstOrDefaultAsync(p => p.ReferenciaPago == model.ReferenciaPago);

            if (pago == null)
            {
                return BadRequest("Pago no encontrado");
            }

            // Verificar que el usuario actual es el propietario del pago
            if (pago.IdUsuario != userId)
            {
                return Forbid();
            }

            // En una aplicación real, aquí se verificaría el estado del pago con el proveedor

            // Actualizar el estado del pago
            pago.Estado = "completado";
            pago.FechaPago = DateTime.Now;
            pago.FechaActualizacion = DateTime.Now;

            // Actualizar el estado de la reserva
            if (pago.Reserva != null)
            {
                pago.Reserva.Estado = "confirmada";
                pago.Reserva.ActualizadaEn = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            // Crear una notificación para el usuario
            var notificacion = new Notificacion
            {
                IdUsuario = userId,
                Titulo = "Pago confirmado",
                Mensaje = $"Tu pago de ${pago.Monto} para la reserva #{pago.IdReserva} ha sido confirmado.",
                Tipo = "pago",
                Leida = false,
                FechaCreacion = DateTime.Now,
                FechaActualizacion = DateTime.Now
            };

            _context.Notificaciones.Add(notificacion);

            // Crear una notificación para el dueño de la cochera
            if (pago.Reserva != null && pago.Reserva.Cochera != null)
            {
                var notificacionDueno = new Notificacion
                {
                    IdUsuario = pago.Reserva.Cochera.IdDueno,
                    Titulo = "Nueva reserva confirmada",
                    Mensaje = $"Una reserva para tu cochera {pago.Reserva.Cochera.Direccion} ha sido confirmada.",
                    Tipo = "reserva",
                    Leida = false,
                    FechaCreacion = DateTime.Now,
                    FechaActualizacion = DateTime.Now
                };

                _context.Notificaciones.Add(notificacionDueno);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Estado = "completado",
                IdPago = pago.IdPago,
                Mensaje = "Pago confirmado correctamente"
            });
        }
    }

    // Modelos adicionales
    public class IniciarPagoModel
    {
        [Required]
        public int IdReserva { get; set; }

        [Required]
        [StringLength(50)]
        public string MetodoPago { get; set; } // tarjeta, transferencia, efectivo
    }

    public class ConfirmarPagoModel
    {
        [Required]
        public string ReferenciaPago { get; set; }
    }

    // Modelo para la tabla de Notificaciones que debería agregarse al contexto
    public class Notificacion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdNotificacion { get; set; }

        [Required]
        [ForeignKey("Usuario")]
        public int IdUsuario { get; set; }

        [Required]
        [StringLength(100)]
        public string Titulo { get; set; }

        [Required]
        public string Mensaje { get; set; }

        [StringLength(50)]
        public string Tipo { get; set; } // reserva, pago, sistema, etc.

        public bool Leida { get; set; } = false;

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public DateTime FechaActualizacion { get; set; } = DateTime.Now;

        // Relaciones
        public virtual Usuario Usuario { get; set; }
    }
}