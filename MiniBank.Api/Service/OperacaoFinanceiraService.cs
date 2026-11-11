using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniBank.Api.Data;
using MiniBank.Api.Models;
using MiniBank.Api.Rules;
using Npgsql;

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

    public async Task<OperacaoFinanceira> TransferirAsync(
        Guid contaOrigemId, Guid contaDestinoId, decimal valor, string? chave, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chave) || chave.Length > 128)
        {
            throw new ErroFinanceiroException(TipoErroFinanceiro.Validacao,
                "Informe o header Idempotency-Key com uma chave não vazia de até 128 caracteres.");
        }

        ValidarContaPublica(contaOrigemId);
        ValidarContaPublica(contaDestinoId);
        Dinheiro.Validar(valor);
        var requestHash = IdempotenciaHash.Gerar(contaOrigemId, contaDestinoId, valor);

        try
        {
            return await ExecutarAsync(TipoOperacao.Transferencia, contaOrigemId, contaDestinoId,
                valor, cancellationToken, chave, requestHash);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_IdemPotencias_Chave"
            })
        {
            // ExecutarAsync já descartou a transação; não consultar dentro da tentativa que falhou.
            context.ChangeTracker.Clear();
            var registro = await BuscarIdempotenciaAsync(chave, cancellationToken);
            if (registro is null)
            {
                throw;
            }

            return await RecuperarOperacaoAsync(registro, requestHash, cancellationToken);
        }
    }

    private async Task<OperacaoFinanceira> ExecutarAsync(
        TipoOperacao tipo, Guid contaDebitoId, Guid contaCreditoId, decimal valor,
        CancellationToken cancellationToken, string? chave = null, string? requestHash = null)
    {
        Dinheiro.Validar(valor);
        if (contaDebitoId == contaCreditoId)
        {
            throw new ErroFinanceiroException(TipoErroFinanceiro.Validacao,
                "As contas de origem e destino devem ser diferentes.");
        }

        await using var transacao = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        if (chave is not null)
        {
            // Sempre bloquear a chave antes das contas, inclusive quando ela ainda não existe.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({chave}, 0))", cancellationToken);

            var registro = await BuscarIdempotenciaAsync(chave, cancellationToken);
            if (registro is not null)
            {
                return await RecuperarOperacaoAsync(registro, requestHash!, cancellationToken);
            }
        }

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
        if (chave is not null)
        {
            context.IdemPotencias.Add(IdemPotenciaRegistro.Criar(chave, requestHash!, operacao.Id));
        }

        await context.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // Iniciado o commit da transferência, a desconexão HTTP não deve interrompê-lo.
        await transacao.CommitAsync(chave is null ? cancellationToken : CancellationToken.None);
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

    private async Task<OperacaoFinanceira> RecuperarOperacaoAsync(
        IdemPotenciaRegistro registro, string requestHash, CancellationToken cancellationToken)
    {
        if (registro.RequestHash != requestHash)
        {
            throw new ErroFinanceiroException(TipoErroFinanceiro.Conflito,
                "A chave de idempotência já foi utilizada com outros dados de transferência.");
        }

        // Reutilizar o histórico confirmado sem validar novamente o saldo atual.
        return await context.OperacoesFinanceiras.AsNoTracking()
            .Include(operacao => operacao.Lancamentos)
            .SingleAsync(operacao => operacao.Id == registro.OperacaoFinanceiraId, cancellationToken);
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
