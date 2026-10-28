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
}