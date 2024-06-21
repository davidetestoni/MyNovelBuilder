﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyNovelBuilder.WebApi.Data;

#nullable disable

namespace MyNovelBuilder.WebApi.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.6");

            modelBuilder.Entity("CompendiumNovel", b =>
                {
                    b.Property<Guid>("CompendiaId")
                        .HasColumnType("TEXT");

                    b.Property<Guid>("NovelsId")
                        .HasColumnType("TEXT");

                    b.HasKey("CompendiaId", "NovelsId");

                    b.HasIndex("NovelsId");

                    b.ToTable("CompendiumNovel");
                });

            modelBuilder.Entity("MyNovelBuilder.WebApi.Data.Entities.Compendium", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Compendia");
                });

            modelBuilder.Entity("MyNovelBuilder.WebApi.Data.Entities.CompendiumRecord", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<string>("Aliases")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("TEXT");

                    b.Property<Guid>("CompendiumId")
                        .HasColumnType("TEXT");

                    b.Property<string>("Context")
                        .IsRequired()
                        .HasMaxLength(10000)
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<Guid?>("CurrentImageId")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("CompendiumId");

                    b.ToTable("CompendiumRecords");
                });

            modelBuilder.Entity("MyNovelBuilder.WebApi.Data.Entities.Novel", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<string>("Author")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("Brief")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<int>("Language")
                        .HasColumnType("INTEGER");

                    b.Property<Guid?>("MainCharacterId")
                        .HasColumnType("TEXT");

                    b.Property<int>("Pov")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Tense")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("MainCharacterId");

                    b.ToTable("Novels");
                });

            modelBuilder.Entity("MyNovelBuilder.WebApi.Data.Entities.Prompt", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Prompts");
                });

            modelBuilder.Entity("CompendiumNovel", b =>
                {
                    b.HasOne("MyNovelBuilder.WebApi.Data.Entities.Compendium", null)
                        .WithMany()
                        .HasForeignKey("CompendiaId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MyNovelBuilder.WebApi.Data.Entities.Novel", null)
                        .WithMany()
                        .HasForeignKey("NovelsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MyNovelBuilder.WebApi.Data.Entities.CompendiumRecord", b =>
                {
                    b.HasOne("MyNovelBuilder.WebApi.Data.Entities.Compendium", "Compendium")
                        .WithMany("Records")
                        .HasForeignKey("CompendiumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Compendium");
                });

            modelBuilder.Entity("MyNovelBuilder.WebApi.Data.Entities.Novel", b =>
                {
                    b.HasOne("MyNovelBuilder.WebApi.Data.Entities.CompendiumRecord", "MainCharacter")
                        .WithMany()
                        .HasForeignKey("MainCharacterId");

                    b.Navigation("MainCharacter");
                });

            modelBuilder.Entity("MyNovelBuilder.WebApi.Data.Entities.Prompt", b =>
                {
                    b.OwnsMany("MyNovelBuilder.WebApi.Models.Prompts.PromptMessage", "Messages", b1 =>
                        {
                            b1.Property<Guid>("PromptId")
                                .HasColumnType("TEXT");

                            b1.Property<int>("Id")
                                .ValueGeneratedOnAdd()
                                .HasColumnType("INTEGER");

                            b1.Property<string>("Message")
                                .IsRequired()
                                .HasMaxLength(50000)
                                .HasColumnType("TEXT");

                            b1.Property<int>("Role")
                                .HasColumnType("INTEGER");

                            b1.HasKey("PromptId", "Id");

                            b1.ToTable("Prompts");

                            b1.ToJson("Messages");

                            b1.WithOwner()
                                .HasForeignKey("PromptId");
                        });

                    b.Navigation("Messages");
                });

            modelBuilder.Entity("MyNovelBuilder.WebApi.Data.Entities.Compendium", b =>
                {
                    b.Navigation("Records");
                });
#pragma warning restore 612, 618
        }
    }
}
