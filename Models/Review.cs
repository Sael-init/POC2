using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CocherasAPI.Models
{
    public class Review
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdReview { get; set; }

        [Required]
        [ForeignKey("Usuario")]
        public int IdUsuario { get; set; }

        [Required]
        [ForeignKey("Cochera")]
        public int IdCochera { get; set; }

        [ForeignKey("Reserva")]
        public int? IdReserva { get; set; }

        [Required]
        [Range(1, 5)]
        public int Calificacion { get; set; }

        public string Comentario { get; set; }

        public DateTime FechaReview { get; set; } = DateTime.Now;

        public DateTime FechaActualizacion { get; set; } = DateTime.Now;

        // Relaciones
        public virtual Usuario Usuario { get; set; }
        public virtual Cochera Cochera { get; set; }
        public virtual Reserva Reserva { get; set; }
    }
}