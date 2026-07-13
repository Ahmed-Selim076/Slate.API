using Microsoft.EntityFrameworkCore;
using Slate.Api.Models;

namespace Slate.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardMember> BoardMembers => Set<BoardMember>();
    public DbSet<ChessGame> ChessGames => Set<ChessGame>();
    public DbSet<CardGame> CardGames => Set<CardGame>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Board>(e =>
        {
            e.Property(b => b.ElementsJson)
                .HasColumnName("elements")
                .HasColumnType("jsonb");

            e.HasOne(b => b.Owner)
                .WithMany()
                .HasForeignKey(b => b.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BoardMember>(e =>
        {
            e.HasKey(m => new { m.BoardId, m.UserId });

            e.HasOne(m => m.Board)
                .WithMany(b => b.Members)
                .HasForeignKey(m => m.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.User)
                .WithMany(u => u.BoardMemberships)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChessGame>(e =>
        {
            e.HasOne(g => g.WhitePlayer)
                .WithMany()
                .HasForeignKey(g => g.WhitePlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(g => g.BlackPlayer)
                .WithMany()
                .HasForeignKey(g => g.BlackPlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CardGame>(e =>
        {
            e.Property(g => g.StateJson)
                .HasColumnName("state")
                .HasColumnType("jsonb");

            e.HasOne(g => g.Player1)
                .WithMany()
                .HasForeignKey(g => g.Player1Id)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(g => g.Player2)
                .WithMany()
                .HasForeignKey(g => g.Player2Id)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
