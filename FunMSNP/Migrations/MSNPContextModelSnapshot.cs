﻿// <auto-generated />
using System;
using FunMSNP;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FunMSNP.Migrations
{
    [DbContext(typeof(MSNPContext))]
    partial class MSNPContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Relational:MaxIdentifierLength", 64)
                .HasAnnotation("ProductVersion", "5.0.1");

            modelBuilder.Entity("FunMSNP.Entities.Contact", b =>
                {
                    b.Property<uint>("ContactID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int unsigned");

                    b.Property<int>("ContactList")
                        .HasColumnType("int");

                    b.Property<uint>("Target")
                        .HasColumnType("int unsigned");

                    b.Property<uint>("User")
                        .HasColumnType("int unsigned");

                    b.HasKey("ContactID");

                    b.ToTable("Contacts");
                });

            modelBuilder.Entity("FunMSNP.Entities.User", b =>
                {
                    b.Property<uint>("ID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int unsigned");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<byte[]>("IV")
                        .IsRequired()
                        .HasColumnType("longblob");

                    b.Property<bool>("MessagePrivacy")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("Nickname")
                        .HasColumnType("longtext CHARACTER SET utf8mb4");

                    b.Property<bool>("Notify")
                        .HasColumnType("tinyint(1)");

                    b.Property<byte[]>("Password")
                        .IsRequired()
                        .HasColumnType("longblob");

                    b.Property<uint>("SyncID")
                        .HasColumnType("int unsigned");

                    b.HasKey("ID");

                    b.ToTable("Users");
                });
#pragma warning restore 612, 618
        }
    }
}
