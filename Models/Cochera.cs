// Cochera.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace CocherasAPI.Models
{
    public class Cochera
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdCochera { get; set; }

        [ForeignKey("Distrito")]
        public int? IdDistrito { get; set; }

        [Required]
        [ForeignKey("Dueno")]
        public int IdDueno { get; set; }

        [Required]
        [StringLength(255)]
        public string Direccion { get; set; }

        public int Capacidad { get; set; } = 1;

        [Required]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal PrecioHora { get; set; }

        public bool Disponible { get; set; } = true;

        public string Descripcion { get; set; }

        public TimeSpan? HoraApertura { get; set; }

        public TimeSpan? HoraCierre { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        public DateTime FechaActualizacion { get; set; } = DateTime.Now;

        // Relaciones
        public virtual Distrito Distrito { get; set; }
        public virtual Usuario Dueno { get; set; }
        public virtual ICollection<Reserva> Reservas { get; set; }
        public virtual ICollection<Review> Reviews { get; set; }
    }
}
