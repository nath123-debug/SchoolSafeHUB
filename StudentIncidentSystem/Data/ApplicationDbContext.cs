using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudentIncidentSystem.Models;

namespace StudentIncidentSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<IncidentCategory> IncidentCategories => Set<IncidentCategory>();
        public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();
        public DbSet<IncidentAttachment> IncidentAttachments => Set<IncidentAttachment>();
        public DbSet<CaseAssignment> CaseAssignments => Set<CaseAssignment>();
        public DbSet<CaseMessage> CaseMessages => Set<CaseMessage>();
        public DbSet<InvestigationNote> InvestigationNotes => Set<InvestigationNote>();
        public DbSet<IncidentStatusHistory> IncidentStatusHistories => Set<IncidentStatusHistory>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<AppSetting> AppSettings => Set<AppSetting>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // required for the Identity tables

            builder.Entity<ApplicationUser>(e =>
            {
                e.HasIndex(u => u.SchoolIdNumber);
            });

            builder.Entity<IncidentCategory>(e =>
            {
                e.HasIndex(c => c.Name).IsUnique();
            });

            builder.Entity<IncidentReport>(e =>
            {
                e.HasIndex(r => r.ReferenceNumber).IsUnique();
                e.HasIndex(r => r.ReporterId);
                e.HasIndex(r => r.CategoryId);
                e.HasIndex(r => r.Status);
                e.HasIndex(r => r.CreatedAtUtc);

                // Store the status as text (e.g. "UnderReview") so it is readable in the database
                e.Property(r => r.Status).HasConversion<string>().HasMaxLength(50);

                e.HasOne(r => r.Reporter).WithMany()
                    .HasForeignKey(r => r.ReporterId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(r => r.Category).WithMany(c => c.Reports)
                    .HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<IncidentAttachment>(e =>
            {
                e.HasIndex(a => a.StoredFileName).IsUnique();
                e.HasIndex(a => a.IncidentReportId);

                e.HasOne(a => a.IncidentReport).WithMany(r => r.Attachments)
                    .HasForeignKey(a => a.IncidentReportId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.UploadedBy).WithMany()
                    .HasForeignKey(a => a.UploadedById).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<CaseAssignment>(e =>
            {
                e.HasIndex(a => a.AssignedToId);

                // Only ONE current assignment per report
                e.HasIndex(a => a.IncidentReportId)
                    .HasFilter("[IsCurrent] = 1")
                    .IsUnique()
                    .HasDatabaseName("UX_CaseAssignments_OneCurrentPerReport");

                e.HasOne(a => a.IncidentReport).WithMany(r => r.Assignments)
                    .HasForeignKey(a => a.IncidentReportId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.AssignedTo).WithMany()
                    .HasForeignKey(a => a.AssignedToId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(a => a.AssignedBy).WithMany()
                    .HasForeignKey(a => a.AssignedById).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<CaseMessage>(e =>
            {
                e.HasIndex(m => new { m.IncidentReportId, m.SentAtUtc });

                e.HasOne(m => m.IncidentReport).WithMany(r => r.Messages)
                    .HasForeignKey(m => m.IncidentReportId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(m => m.Sender).WithMany()
                    .HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<InvestigationNote>(e =>
            {
                e.HasIndex(n => n.IncidentReportId);
                e.Property(n => n.NoteType).HasConversion<string>().HasMaxLength(30);

                e.HasOne(n => n.IncidentReport).WithMany(r => r.InvestigationNotes)
                    .HasForeignKey(n => n.IncidentReportId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(n => n.Author).WithMany()
                    .HasForeignKey(n => n.AuthorId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<IncidentStatusHistory>(e =>
            {
                e.HasIndex(h => new { h.IncidentReportId, h.ChangedAtUtc });
                e.Property(h => h.OldStatus).HasConversion<string>().HasMaxLength(50);
                e.Property(h => h.NewStatus).HasConversion<string>().HasMaxLength(50);

                e.HasOne(h => h.IncidentReport).WithMany(r => r.StatusHistory)
                    .HasForeignKey(h => h.IncidentReportId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(h => h.ChangedBy).WithMany()
                    .HasForeignKey(h => h.ChangedById).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AuditLog>(e =>
            {
                e.HasIndex(l => l.TimestampUtc);
                e.HasIndex(l => l.UserId);
            });
        }
    }
}
