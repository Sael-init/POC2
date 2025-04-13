using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace CocherasAPI.Models
{
    public class Reserva
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdReserva { get; set; }

        [Required]
        [ForeignKey("Usuario")]
        public int IdUsuario { get; set; }

        [Required]
        [ForeignKey("Cochera")]
        public int IdCochera { get; set; }

        [Required]
        public DateTime FechaInicio { get; set; }

        [Required]
        public DateTime FechaFin { get; set; }

        [StringLength(20)]
        public string Estado { get; set; } = "pendiente"; // pendiente, confirmada, cancelada, completada

        public DateTime CreadaEn { get; set; } = DateTime.Now;

        public DateTime ActualizadaEn { get; set; } = DateTime.Now;

        // Relaciones
        public virtual Usuario Usuario { get; set; }
        public virtual Cochera Cochera { get; set; }
        public virtual ICollection<Pago> Pagos { get; set; }
        public virtual ICollection<Review> Reviews { get; set; }
    }
}