using Microsoft.EntityFrameworkCore;
using System;

namespace TutorLiveMentor.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Student> Students { get; set; }
        public DbSet<Faculty> Faculties { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<AssignedSubject> AssignedSubjects { get; set; }
        public DbSet<StudentEnrollment> StudentEnrollments { get; set; }
        public DbSet<Admin> Admins { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Simple, clean configuration - let EF Core use conventions
            
            // AssignedSubject -> Subject (many-to-one)
            modelBuilder.Entity<AssignedSubject>()
                .HasOne(a => a.Subject)
                .WithMany(s => s.AssignedSubjects)
                .HasForeignKey(a => a.SubjectId);

            // AssignedSubject -> Faculty (many-to-one)
            modelBuilder.Entity<AssignedSubject>()
                .HasOne(a => a.Faculty)
                .WithMany(f => f.AssignedSubjects)
                .HasForeignKey(a => a.FacultyId);

            // Configure the many-to-many relationship
            modelBuilder.Entity<StudentEnrollment>()
                .HasKey(se => new { se.StudentId, se.AssignedSubjectId });

            modelBuilder.Entity<StudentEnrollment>()
                .HasOne(se => se.Student)
                .WithMany(s => s.Enrollments)
                .HasForeignKey(se => se.StudentId);

            modelBuilder.Entity<StudentEnrollment>()
                .HasOne(se => se.AssignedSubject)
                .WithMany(a => a.Enrollments)
                .HasForeignKey(se => se.AssignedSubjectId);

            // Admin configuration
            modelBuilder.Entity<Admin>()
                .HasIndex(a => a.Email)
                .IsUnique();

            // Seed the specific admin data you requested
            modelBuilder.Entity<Admin>().HasData(
                new Admin
                {
                    AdminId = 1,
                    Email = "cseds@rgmcet.edu.in",
                    Password = "admin123",
                    Department = "CSEDS"
                }
            );
        }
    }
}
