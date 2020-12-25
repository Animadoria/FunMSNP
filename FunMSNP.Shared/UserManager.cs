using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FunMSNP.Entities;
using FunMSNP.Shared;
using Microsoft.Extensions.Configuration;

namespace FunMSNP.Shared
{
    public class UserManager
    {
        private readonly MSNPContext db;
        private readonly IConfiguration config;

        public UserManager(IServiceProvider sp, IConfiguration configuration)
        {
            db = (MSNPContext)sp.GetService(typeof(MSNPContext));
            config = configuration;
        }

        public async Task CreateUserAsync(string email, string password)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(config.GetValue<string>("AES-Key"));

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using MemoryStream msEncrypt = new();
            using CryptoStream csEncrypt = new(msEncrypt, encryptor, CryptoStreamMode.Write);
            using (StreamWriter swEncrypt = new(csEncrypt))
            {
                swEncrypt.Write(password);
            }

            var encryptedPassword = msEncrypt.ToArray();

            await db.Users.AddAsync(new User
            {
                Email = email,
                Password = encryptedPassword,
                IV = aes.IV
            });
            await db.SaveChangesAsync();
        }

        public string GetPassword(User user)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(config.GetValue<string>("AES-Key"));
            aes.IV = user.IV;

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using MemoryStream msDecrypt = new(user.Password);
            using CryptoStream csDecrypt = new(msDecrypt, decryptor, CryptoStreamMode.Read);
            using StreamReader srDecrypt = new(csDecrypt);

            var plaintext = srDecrypt.ReadToEnd();
            return plaintext;
        }

        public bool AreUsersInGroup(ContactList cl, User user, User target)
        {
            return db.Contacts.AsEnumerable().Where(x => x.User == user.ID
                        && x.Target == target.ID
                        && x.ContactList == cl).Any();
        }
    }
}
