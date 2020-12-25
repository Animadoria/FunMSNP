using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FunMSNP.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FunMSNP.Shared
{
    public class MSNPContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Contact> Contacts { get; set; }

        public MSNPContext(DbContextOptions options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Contact>()
                .HasKey(x => x.ContactID);
        }
    }

    class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MSNPContext>
    {
        public MSNPContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var builder = new DbContextOptionsBuilder<MSNPContext>();
            var conn = configuration.GetConnectionString("Database");

            builder.UseMySql(conn, ServerVersion.AutoDetect(conn));
            return new MSNPContext(builder.Options);
        }
    }
}
