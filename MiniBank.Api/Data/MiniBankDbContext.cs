using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Models;

namespace MiniBank.Api.Data;

public class MiniBankDbContext : DbContext
{
    public MiniBankDbContext(
        DbContextOptions<MiniBankDbContext> options
    ) : base(options)
    {
    }
    
    public DbSet<Conta> Contas { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Conta>()
            .HasIndex(c => c.Numero)
            .IsUnique();
    }
}