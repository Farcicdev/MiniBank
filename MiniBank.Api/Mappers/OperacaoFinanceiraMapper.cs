using MiniBank.Api.DTOs;
using MiniBank.Api.Models;

namespace MiniBank.Api.Mappers;

public static class OperacaoFinanceiraMapper
{
    public static OperacaoFinanceiraResponse ToResponse(OperacaoFinanceira operacao) => new(
        operacao.Id, operacao.Tipo.ToString(), operacao.ContaDebitoId, operacao.ContaCreditoId,
        operacao.Valor, operacao.DataUtc,
        operacao.Lancamentos.Select(l =>
            new LancamentoResponse(l.Id, l.ContaId, l.Natureza.ToString(), l.Valor)).ToList());
}
