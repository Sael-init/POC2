using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CocherasAPI.Models
{
    public class DuenoCochera
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Usuario")]
        public int IdUsuario { get; set; }

        [Required]
        [ForeignKey("Cochera")]
        public int IdCochera { get; set; }

        public DateTime FechaAsignacion { get; set; } = DateTime.Now;

        // Relaciones
        public virtual Usuario Usuario { get; set; }
        public virtual Cochera Cochera { get; set; }
    }
}
