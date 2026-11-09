using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Data;
using MiniBank.Api.Models;
using MiniBank.Api.Rules;

namespace MiniBank.Api.Service;

public class OperacaoFinanceiraService(MiniBankDbContext context)
{
    public Task<OperacaoFinanceira> DepositarAsync(
        Guid contaId, decimal valor, CancellationToken cancellationToken = default)
    {
        ValidarContaPublica(contaId);
        return ExecutarAsync(TipoOperacao.DepositoSimulado, Conta.CompensacaoId, contaId,
            valor, cancellationToken);
    }

    public Task<OperacaoFinanceira> TransferirAsync(
        Guid contaOrigemId, Guid contaDestinoId, decimal valor, string Chave, CancellationToken cancellationToken = default)
    {
        ValidarContaPublica(contaOrigemId);
        ValidarContaPublica(contaDestinoId);
        return ExecutarAsync(TipoOperacao.Transferencia, contaOrigemId, contaDestinoId,
            valor, cancellationToken);
    }

    private async Task<OperacaoFinanceira> ExecutarAsync(
        TipoOperacao tipo, Guid contaDebitoId, Guid contaCreditoId, decimal valor,
        CancellationToken cancellationToken)
    {
        Dinheiro.Validar(valor);
        if (contaDebitoId == contaCreditoId)
        {
            throw new ErroFinanceiroException(TipoErroFinanceiro.Validacao,
                "As contas de origem e destino devem ser diferentes.");
        }

        await using var transacao = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        // Parâmetros interpolados são enviados como parâmetros SQL pelo EF Core.
        // A ordenação no PostgreSQL é a mesma para todas as operações, inclusive depósitos.
        // Não usar AsNoTracking: estas entidades serão alteradas sob o bloqueio.
        var contas = await context.Contas.FromSqlInterpolated($"""
            SELECT * FROM "Contas"
            WHERE "Id" = {contaDebitoId} OR "Id" = {contaCreditoId}
            ORDER BY "Id"
            FOR UPDATE
            """).ToListAsync(cancellationToken);

        var debito = contas.SingleOrDefault(c => c.Id == contaDebitoId);
        var credito = contas.SingleOrDefault(c => c.Id == contaCreditoId);
        if (tipo == TipoOperacao.DepositoSimulado && debito is null)
        {
            throw new InvalidOperationException("A conta de compensação não foi provisionada pela migration.");
        }

        if (debito is null || credito is null)
        {
            throw new ErroFinanceiroException(TipoErroFinanceiro.ContaNaoEncontrada,
                "Uma ou mais contas não foram encontradas.");
        }

        // A validação de saldo ocorre somente depois da aquisição dos dois bloqueios.
        var operacao = OperacaoFinanceira.Criar(tipo, debito, credito, valor);
        context.OperacoesFinanceiras.Add(operacao);
        await context.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return operacao;
        // Sem commit, DisposeAsync desfaz a transação, inclusive em cancelamento/erro.
    }
    
    //idempotencia
    private async Task<IdemPotenciaRegistro?> BuscarIdempotenciaAsync(string chave, CancellationToken cancellationToken)
    {
        return await context.IdemPotencias
            .AsNoTracking()
            .SingleOrDefaultAsync(registro =>
                    registro.Chave == chave,
                cancellationToken);
    }

    private static void ValidarContaPublica(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ErroFinanceiroException(TipoErroFinanceiro.Validacao, "Informe um identificador de conta válido.");
        }

        if (id == Conta.CompensacaoId)
        {
            throw new ErroFinanceiroException(TipoErroFinanceiro.ContaNaoEncontrada, "Conta não encontrada.");
        }
    }
}
