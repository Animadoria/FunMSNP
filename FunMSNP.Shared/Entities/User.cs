using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FunMSNP.Entities
{
    public class User
    {
        [Required, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public uint ID { get; set; }

        [Required]
        public string Email { get; set; }

        [Required]
        public byte[] Password { get; set; }

        [Required]
        public byte[] IV { get; set; }

        [Required]
        public uint SyncID { get; set; } = 0;

        [Required]
        public bool Notify { get; set; } = true;

        [Required]
        public bool MessagePrivacy { get; set; } = true;

        public string Nickname { get; set; }

        [NotMapped]
        public string SafeNickname
        {
            get
            {
                return (string.IsNullOrWhiteSpace(this.Nickname) ? this.Email : this.Nickname);
            }
        }
    }
}
