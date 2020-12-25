using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FunMSNP.Shared;
using FunMSNP.Site.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FunMSNP.Site.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager _userManager;
        private readonly MSNPContext _context;

        public RegisterModel(UserManager userManager, MSNPContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        public Registry Registry { get; set; }

        public void OnGet()
        {

        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (await _context.Users.Where(x => x.Email == Registry.EmailAddress.ToLower()).AnyAsync())
            {
                ViewData["Error"] = "An account with that e-mail address already exists.";
                return Page();
            }

            try
            {
                await _userManager.CreateUserAsync(Registry.EmailAddress, Registry.Password);
            }
            catch
            {
                ViewData["Error"] = "An error occurred while creating the account. Contact Animadoria immediately!";
                return Page();
            }

            ViewData["Success"] = true;
            return Page();
        }
    }
}
