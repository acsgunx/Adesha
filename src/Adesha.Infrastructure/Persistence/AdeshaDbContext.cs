using Adesha.Domain.Auditing;
using Adesha.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Adesha.Infrastructure.Persistence;

public sealed class AdeshaDbContext(DbContextOptions<AdeshaDbContext> options)
    : IdentityDbContext<AdeshaUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AuditRecord>(audit =>
        {
            audit.ToTable("audit_records");
            audit.HasKey(a => a.Id);
            audit.Property(a => a.Actor).HasMaxLength(256).IsRequired();
            audit.Property(a => a.Action).HasMaxLength(256).IsRequired();
            audit.Property(a => a.EntityType).HasMaxLength(256).IsRequired();
            audit.Property(a => a.EntityId).HasMaxLength(256).IsRequired();
            audit.Property(a => a.CorrelationId).HasMaxLength(128).IsRequired();
            audit.Property(a => a.BrokerRequestId).HasMaxLength(256);
            audit.Property(a => a.BeforeState).HasColumnType("jsonb");
            audit.Property(a => a.AfterState).HasColumnType("jsonb");
            audit.HasIndex(a => a.OccurredAtUtc);
            audit.HasIndex(a => new { a.EntityType, a.EntityId });
        });

        builder.Entity<RefreshToken>(token =>
        {
            token.ToTable("refresh_tokens");
            token.HasKey(t => t.Id);
            token.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasIndex(t => t.UserId);
            token.HasOne<AdeshaUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
