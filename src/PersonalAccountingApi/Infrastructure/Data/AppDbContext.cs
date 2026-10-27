using Microsoft.EntityFrameworkCore;
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
}