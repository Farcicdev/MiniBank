namespace MiniBank.Api.DTOs;

public record LancamentoResponse(Guid Id, Guid ContaId, string Natureza, decimal Valor);

public record OperacaoFinanceiraResponse(
    Guid Id, string Tipo, Guid ContaDebitoId, Guid ContaCreditoId,
    decimal Valor, DateTime DataUtc, IReadOnlyCollection<LancamentoResponse> Lancamentos);
