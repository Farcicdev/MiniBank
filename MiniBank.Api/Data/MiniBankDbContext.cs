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
    public DbSet<OperacaoFinanceira> OperacoesFinanceiras { get; set; }
    public DbSet<Lancamento> Lancamentos { get; set; }
    
    //idempotencia
    public DbSet<IdemPotenciaRegistro> IdemPotencias { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Conta>()
            .HasIndex(c => c.Numero)
            .IsUnique();

        modelBuilder.Entity<Conta>(conta =>
        {
            conta.Property(c => c.Saldo).HasPrecision(18, 2);
            conta.ToTable("Contas", tabela => tabela.HasCheckConstraint("CK_Contas_SaldoETipo", """
                ("EhCompensacao" AND "Id" = '00000000-0000-0000-0000-000000000001'
                    AND "Numero" = '__COMPENSACAO_DEPOSITOS__' AND "Saldo" <= 0)
                OR (NOT "EhCompensacao" AND "Id" <> '00000000-0000-0000-0000-000000000001'
                    AND "Saldo" >= 0)
                """));
        });

        modelBuilder.Entity<OperacaoFinanceira>(operacao =>
        {
            operacao.Property(o => o.Valor).HasPrecision(18, 2);
            operacao.Property(o => o.DataUtc).HasColumnType("timestamp with time zone");
            operacao.HasOne<Conta>().WithMany().HasForeignKey(o => o.ContaDebitoId)
                .OnDelete(DeleteBehavior.Restrict);
            operacao.HasOne<Conta>().WithMany().HasForeignKey(o => o.ContaCreditoId)
                .OnDelete(DeleteBehavior.Restrict);
            operacao.HasMany(o => o.Lancamentos).WithOne().HasForeignKey(l => l.OperacaoId)
                .OnDelete(DeleteBehavior.Restrict);
            operacao.Navigation(o => o.Lancamentos).UsePropertyAccessMode(PropertyAccessMode.Field);
            operacao.ToTable("OperacoesFinanceiras", tabela =>
            {
                tabela.HasCheckConstraint("CK_Operacoes_Valor", "\"Valor\" > 0");
                tabela.HasCheckConstraint("CK_Operacoes_Contas", "\"ContaDebitoId\" <> \"ContaCreditoId\"");
                tabela.HasCheckConstraint("CK_Operacoes_Tipo", "\"Tipo\" IN (1, 2, 3)");
                tabela.HasCheckConstraint("CK_Operacoes_Compensacao", """
                    "ContaCreditoId" <> '00000000-0000-0000-0000-000000000001'
                    AND (("Tipo" IN (1, 3) AND "ContaDebitoId" = '00000000-0000-0000-0000-000000000001')
                        OR ("Tipo" = 2 AND "ContaDebitoId" <> '00000000-0000-0000-0000-000000000001'))
                    """);
            });
        });

        modelBuilder.Entity<Lancamento>(lancamento =>
        {
            lancamento.Property(l => l.Valor).HasPrecision(18, 2);
            lancamento.HasOne<Conta>().WithMany().HasForeignKey(l => l.ContaId)
                .OnDelete(DeleteBehavior.Restrict);
            lancamento.HasIndex(l => new { l.OperacaoId, l.Natureza }).IsUnique();
            lancamento.ToTable("Lancamentos", tabela =>
            {
                tabela.HasCheckConstraint("CK_Lancamentos_Valor", "\"Valor\" > 0");
                tabela.HasCheckConstraint("CK_Lancamentos_Natureza", "\"Natureza\" IN (1, 2)");
            });
        });
        
        //idempotencia
        modelBuilder.Entity<IdemPotenciaRegistro>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Chave)
                .IsUnique();

            entity.HasOne<OperacaoFinanceira>()
                .WithMany()
                .HasForeignKey(x => x.OperacaoFinanceiraId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
