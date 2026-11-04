namespace MiniBank.Api.Models;

public enum TipoErroFinanceiro
{
    Validacao,
    ContaNaoEncontrada,
    Conflito
}

public sealed class ErroFinanceiroException(TipoErroFinanceiro tipo, string mensagem) : Exception(mensagem)
{
    public TipoErroFinanceiro Tipo { get; } = tipo;
}
