using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace CocherasAPI.Models
{
    public class Usuario
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdUsuario { get; set; }

        [Required]
        [StringLength(100)]
        public string Nombre { get; set; }

        [Required]
        [StringLength(100)]
        public string Apellido { get; set; }

        [Required]
        [StringLength(255)]
        [EmailAddress]
        public string Email { get; set; }

        [StringLength(20)]
        public string Telefono { get; set; }

        [Required]
        [StringLength(255)]
        public string Contrasena { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        public DateTime? UltimaConexion { get; set; }

        [StringLength(20)]
        public string Estado { get; set; } = "activo";

        public DateTime FechaActualizacion { get; set; } = DateTime.Now;

        // Relaciones
        public virtual ICollection<Reserva> Reservas { get; set; }
        public virtual ICollection<Cochera> Cocheras { get; set; }
        public virtual ICollection<Pago> Pagos { get; set; }
        public virtual ICollection<Review> Reviews { get; set; }
    }
}
