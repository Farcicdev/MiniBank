namespace MiniBank.Api.Models;

public enum NaturezaLancamento
{
    Debito = 1,
    Credito = 2
}

public class Lancamento
{
    private Lancamento() { }

    internal Lancamento(Guid operacaoId, Guid contaId, NaturezaLancamento natureza, decimal valor)
    {
        Id = Guid.NewGuid();
        OperacaoId = operacaoId;
        ContaId = contaId;
        Natureza = natureza;
        Valor = valor;
    }

    public Guid Id { get; private set; }
    public Guid OperacaoId { get; private set; }
    public Guid ContaId { get; private set; }
    public NaturezaLancamento Natureza { get; private set; }
    public decimal Valor { get; private set; }
}
