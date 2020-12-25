using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FunMSNP.Entities
{
    public class Contact
    {
        [Required, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public uint ContactID { get; }

        [Required]
        public ContactList ContactList { get; set; }

        [Required]
        public uint User { get; set; }

        [Required]
        public uint Target { get; set; }
    }
}