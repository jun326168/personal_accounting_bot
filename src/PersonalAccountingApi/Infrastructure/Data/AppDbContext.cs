using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalAccountingApi.Domain.Entities;

namespace PersonalAccountingApi.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
  public DbSet<User> Users => Set<User>();
  public DbSet<Message> Messages => Set<Message>();
  public DbSet<Transaction> Transactions => Set<Transaction>();
  public DbSet<Account> Accounts => Set<Account>();
  public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
  public DbSet<Proposal> Proposals => Set<Proposal>();

  private static string ToSnake(string s) 
    => string.Concat(s.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLower(c) : char.ToLower(c).ToString()));

  protected override void OnModelCreating(ModelBuilder b)
  {
    b.HasDefaultSchema("app");

    // enums are stored as lowercase text. flex_message etc. come out right with the snake_case naming
    b.Entity<Message>(e =>
    {
      e.Property(x => x.Role).HasConversion(v => v.ToString().ToLowerInvariant(), v => Enum.Parse<MessageRole>(v, true));
      e.Property(x => x.Type).HasConversion(v => ToSnake(v.ToString()), v => Enum.Parse<MessageType>(v.Replace("_", ""), true));
      e.Property(x => x.JsonContent).HasColumnType("jsonb");
    });
    b.Entity<Transaction>().Property(x => x.Kind)
      .HasConversion(v => v.ToString().ToLowerInvariant(), v => Enum.Parse<TransactionKind>(v, true));
    b.Entity<Account>(e =>
    {
      e.Property(x => x.Type).HasConversion(v => v.ToString().ToLowerInvariant(), v => Enum.Parse<AccountType>(v, true));
      e.Property(x => x.NormalBalance)
        .HasConversion(v => v.ToString().ToLowerInvariant(), v => Enum.Parse<NormalBalance>(v, true))
        .ValueGeneratedOnAddOrUpdate();
    });
    b.Entity<Proposal>(e =>
    {
      e.Property(x => x.Status).HasConversion(v => v.ToString().ToLowerInvariant(), v => Enum.Parse<ProposalStatus>(v, true));
      e.Property(x => x.Content).HasColumnType("jsonb");
    });

    // let the db fill created_at instead of EF sending 0001-01-01
    foreach (var entity in b.Model.GetEntityTypes())
      if (entity.FindProperty("CreatedAt") is { } p)
        p.SetDefaultValueSql("now()");
  }
}