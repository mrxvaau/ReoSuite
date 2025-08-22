
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace Reo.Studio.Data
{
    public class StudioDb : DbContext
    {
        public DbSet<ProgramRecord> Programs => Set<ProgramRecord>();
        public DbSet<BuildRecord> Builds => Set<BuildRecord>();
        public DbSet<RunRecord> Runs => Set<RunRecord>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReoStudio", "reo.db");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            optionsBuilder.UseSqlite($"Data Source={path}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProgramRecord>().HasIndex(p => p.FilePath).IsUnique(false);
        }

        public ProgramRecord UpsertProgram(string filePath, string source)
        {
            var existing = GetProgramByPath(filePath);
            if (existing is null)
            {
                var p = new ProgramRecord { Name = System.IO.Path.GetFileName(filePath), FilePath = filePath, Source = source, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                Programs.Add(p);
                SaveChanges();
                return p;
            }
            else
            {
                existing.Source = source;
                existing.UpdatedAt = DateTime.UtcNow;
                SaveChanges();
                return existing;
            }
        }

        public ProgramRecord? GetProgramByPath(string filePath)
        {
            return Programs.FirstOrDefault(p => p.FilePath == filePath);
        }
    }

    public class ProgramRecord
    {
        [Key] public int Id { get; set; }
        public string Name { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string Source { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class BuildRecord
    {
        [Key] public int Id { get; set; }
        public int ProgramRecordId { get; set; }
        public string OutputPath { get; set; } = "";
        public bool Success { get; set; }
        public string Diagnostics { get; set; } = "";
        public DateTime BuiltAt { get; set; }
    }

    public class RunRecord
    {
        [Key] public int Id { get; set; }
        public int ProgramRecordId { get; set; }
        public string Output { get; set; } = "";
        public int ExitCode { get; set; }
        public DateTime RanAt { get; set; }
    }
}
