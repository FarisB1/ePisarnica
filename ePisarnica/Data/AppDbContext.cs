using ePisarnica.Models;
using Microsoft.EntityFrameworkCore;

namespace ePisarnica.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<ProtocolEntry> ProtocolEntries { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Folder> Folders { get; set; }
        public DbSet<Assignment> Assignments { get; set; }
        public DbSet<Department> Departments { get; set; }

        // Novi DbSetovi za digitalni potpis
        public DbSet<DigitalSignature> DigitalSignatures { get; set; }
        public DbSet<SignatureCertificate> SignatureCertificates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Username).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Ime).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Prezime).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(100);
                entity.Property(u => u.PasswordHash).IsRequired();
                entity.Property(u => u.Role).IsRequired().HasDefaultValue("User");

                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();

                // Relationships - keep cascade delete for User->Documents
                entity.HasMany(u => u.Documents)
                      .WithOne(d => d.User)
                      .HasForeignKey(d => d.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(u => u.Folders)
                      .WithOne(f => f.User)
                      .HasForeignKey(f => f.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Nove veze za digitalni potpis - NO ACTION umjesto Cascade
                entity.HasMany(u => u.DigitalSignatures)
                      .WithOne(ds => ds.User)
                      .HasForeignKey(ds => ds.UserId)
                      .OnDelete(DeleteBehavior.NoAction); // Promijenjeno u NoAction

                entity.HasMany(u => u.SignatureCertificates)
                      .WithOne(sc => sc.User)
                      .HasForeignKey(sc => sc.UserId)
                      .OnDelete(DeleteBehavior.NoAction); // Promijenjeno u NoAction
            });

            // Document configuration
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Title).IsRequired().HasMaxLength(200);
                entity.Property(d => d.FilePath).IsRequired();
                entity.Property(d => d.FileName).IsRequired();
                entity.Property(d => d.FileSize).IsRequired();
                entity.Property(d => d.FileExtension).IsRequired();
                entity.Property(d => d.FileType).IsRequired().HasConversion<string>();
                entity.Property(d => d.Status).IsRequired().HasConversion<string>().HasDefaultValue(DocumentStatus.Zaprimljeno);

                entity.Property(d => d.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(d => d.ModifiedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(d => d.IsTrashed).HasDefaultValue(false);
                entity.Property(d => d.IsShared).HasDefaultValue(false);

                // Relationships
                entity.HasOne(d => d.User)
                      .WithMany(u => u.Documents)
                      .HasForeignKey(d => d.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Change Folder relationship to NO ACTION to avoid multiple cascade paths
                entity.HasOne(d => d.Folder)
                      .WithMany(f => f.Documents)
                      .HasForeignKey(d => d.FolderId)
                      .OnDelete(DeleteBehavior.NoAction);

                // Nova veza za digitalne potpise - NO ACTION umjesto Cascade
                modelBuilder.Entity<DigitalSignature>()
                    .HasOne(ds => ds.Document)
                    .WithMany() // or .WithMany(d => d.DigitalSignatures)
                    .HasForeignKey(ds => ds.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Assignment configuration
            modelBuilder.Entity<Assignment>(entity =>
            {
                entity.HasOne(a => a.ProtocolEntry)
                    .WithMany()
                    .HasForeignKey(a => a.ProtocolEntryId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.DodijeljenOdjel)
                    .WithMany(d => d.Assignments)
                    .HasForeignKey(a => a.DodijeljenOdjelId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.DodijeljenUser)
                    .WithMany(u => u.Assignments)
                    .HasForeignKey(a => a.DodijeljenUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Department configuration
            modelBuilder.Entity<User>()
                .HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Folder configuration
            modelBuilder.Entity<Folder>(entity =>
            {
                entity.HasKey(f => f.Id);
                entity.Property(f => f.Name).IsRequired().HasMaxLength(100);
                entity.Property(f => f.Color).HasDefaultValue("#6c757d");
                entity.Property(f => f.CreatedAt).HasDefaultValueSql("GETDATE()");

                // Self-referencing relationship for subfolders
                entity.HasOne(f => f.ParentFolder)
                      .WithMany()
                      .HasForeignKey(f => f.ParentFolderId)
                      .OnDelete(DeleteBehavior.NoAction);

                // Relationships
                entity.HasOne(f => f.User)
                      .WithMany(u => u.Folders)
                      .HasForeignKey(f => f.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(f => f.Documents)
                      .WithOne(d => d.Folder)
                      .HasForeignKey(d => d.FolderId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // DigitalSignature configuration
            modelBuilder.Entity<DigitalSignature>(entity =>
            {
                entity.HasKey(ds => ds.Id);

                // Veza sa Document - NO ACTION
                modelBuilder.Entity<DigitalSignature>()
                    .HasOne(ds => ds.Document)
                    .WithMany(d => d.DigitalSignatures) // if navigation property exists
                    .HasForeignKey(ds => ds.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);


                // Veza sa User - NO ACTION
                entity.HasOne(ds => ds.User)
                    .WithMany(u => u.DigitalSignatures)
                    .HasForeignKey(ds => ds.UserId)
                    .OnDelete(DeleteBehavior.NoAction); // Promijenjeno u NoAction

                // Indeksi za brže pretraživanje
                entity.HasIndex(ds => ds.DocumentId);
                entity.HasIndex(ds => ds.UserId);
                entity.HasIndex(ds => ds.SignedAt);
                entity.HasIndex(ds => ds.IsValid);

                // Ograničenja za stringove
                entity.Property(ds => ds.SignatureData)
                    .IsRequired()
                    .HasColumnType("nvarchar(max)");

                entity.Property(ds => ds.SignatureHash)
                    .IsRequired()
                    .HasMaxLength(44); // Base64 SHA256 hash

                entity.Property(ds => ds.Reason)
                    .HasMaxLength(500);

                entity.Property(ds => ds.Location)
                    .HasMaxLength(500);

                // Default vrijednosti
                entity.Property(ds => ds.SignedAt)
                    .HasDefaultValueSql("GETDATE()");

                entity.Property(ds => ds.IsValid)
                    .HasDefaultValue(true);
            });

            // SignatureCertificate configuration
            modelBuilder.Entity<SignatureCertificate>(entity =>
            {
                entity.HasKey(sc => sc.Id);

                // Veza sa User - NO ACTION
                entity.HasOne(sc => sc.User)
                    .WithMany(u => u.SignatureCertificates)
                    .HasForeignKey(sc => sc.UserId)
                    .OnDelete(DeleteBehavior.NoAction); // Promijenjeno u NoAction

                // Indeksi
                entity.HasIndex(sc => sc.UserId);
                entity.HasIndex(sc => sc.ExpiresAt);
                entity.HasIndex(sc => sc.IsRevoked);

                // Ograničenja za stringove
                entity.Property(sc => sc.PublicKey)
                    .IsRequired()
                    .HasColumnType("nvarchar(max)");

                entity.Property(sc => sc.RevocationReason)
                    .HasMaxLength(500);

                // Default vrijednosti
                entity.Property(sc => sc.IssuedAt)
                    .HasDefaultValueSql("GETDATE()");

                entity.Property(sc => sc.ExpiresAt)
                    .HasDefaultValueSql("DATEADD(year, 1, GETDATE())");

                entity.Property(sc => sc.IsRevoked)
                    .HasDefaultValue(false);
            });

            // ProtocolEntry configuration
            modelBuilder.Entity<ProtocolEntry>(entity =>
            {
                // Ako želite povezati ProtocolEntry sa Document za potpise
                modelBuilder.Entity<ProtocolEntry>()
                    .HasOne(p => p.Document)
                    .WithMany() // or .WithMany(d => d.ProtocolEntries) if navigation exists
                    .HasForeignKey(p => p.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}