using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CocherasAPI.Models
{
    public class Pago
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdPago { get; set; }

        [Required]
        [ForeignKey("Reserva")]
        public int IdReserva { get; set; }

        [Required]
        [ForeignKey("Usuario")]
        public int IdUsuario { get; set; }

        [Required]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal Monto { get; set; }

        [Required]
        [StringLength(50)]
        public string MetodoPago { get; set; } // tarjeta, transferencia, efectivo

        [StringLength(255)]
        public string ReferenciaPago { get; set; }

        [StringLength(20)]
        public string Estado { get; set; } = "pendiente"; // pendiente, completado, fallido, reembolsado

        public DateTime? FechaPago { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public DateTime FechaActualizacion { get; set; } = DateTime.Now;

        // Relaciones
        public virtual Reserva Reserva { get; set; }
        public virtual Usuario Usuario { get; set; }
    }
}
