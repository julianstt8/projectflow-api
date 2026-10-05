using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    /// <summary>Hex-encoded SHA-256.</summary>
    private const int TokenHashLength = 64;

    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        builder.Property(token => token.UserId);
        builder.Property(token => token.FamilyId);
        builder.Property(token => token.TokenHash).HasMaxLength(TokenHashLength).IsFixedLength();
        builder.Property(token => token.CreatedAt);
        builder.Property(token => token.ExpiresAt);
        builder.Property(token => token.RevokedAt);
        builder.Property(token => token.ReplacedById);
        builder.Ignore(token => token.IsRevoked);

        // Two requests rotating the same token at the same time: only the first one can save.
        builder.Property<uint>(PersistenceConstants.RowVersion).IsRowVersion();

        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.FamilyId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
