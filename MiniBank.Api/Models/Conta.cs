using MiniBank.Api.Rules;

namespace MiniBank.Api.Models;

public class Conta
{
    public static readonly Guid CompensacaoId = new("00000000-0000-0000-0000-000000000001");

    public Guid Id { get; set; }

    public string Numero { get; set; } = string.Empty;
    
    public decimal Saldo { get; private set; }

    public bool EhCompensacao { get; private set; }

    internal void Aplicar(Lancamento lancamento)
    {
        Dinheiro.Validar(lancamento.Valor);
        if (lancamento.ContaId != Id)
        {
            throw new InvalidOperationException("O lançamento não pertence à conta.");
        }

        // Subledger de natureza credora: crédito aumenta saldo; débito diminui.
        var novoSaldo = lancamento.Natureza switch
        {
            NaturezaLancamento.Debito => Saldo - lancamento.Valor,
            NaturezaLancamento.Credito => Saldo + lancamento.Valor,
            _ => throw new InvalidOperationException("Natureza de lançamento inválida.")
        };

        if (!EhCompensacao && novoSaldo < 0)
        {
            throw new ErroFinanceiroException(TipoErroFinanceiro.Conflito, "Saldo insuficiente.");
        }

        if (novoSaldo > Dinheiro.ValorMaximo || novoSaldo < -Dinheiro.ValorMaximo)
        {
            throw new ErroFinanceiroException(TipoErroFinanceiro.Conflito,
                "A operação excede o limite de saldo permitido.");
        }

        Saldo = novoSaldo;
    }
}
