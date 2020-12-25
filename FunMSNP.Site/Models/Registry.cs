// File Registry.cs created by Animadoria (me@animadoria.cf) at 12/25/2020 6:53 PM.
// (C) Animadoria 2020 - All Rights Reserved
using System;
using System.ComponentModel.DataAnnotations;

namespace FunMSNP.Site.Models
{
    public class Registry
    {
        [EmailAddress]
        [Required]
        public string EmailAddress { get; set; }

        [StringLength(64, MinimumLength = 6)]
        [Required]
        public string Password { get; set; }
    }
}
