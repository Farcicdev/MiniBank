using MiniBank.Api.Rules;

namespace MiniBank.Api.Models;

public enum TipoOperacao
{
    DepositoSimulado = 1,
    Transferencia = 2,
    SaldoInicial = 3
}

public class OperacaoFinanceira
{
    private readonly List<Lancamento> _lancamentos = [];

    private OperacaoFinanceira() { }

    public Guid Id { get; private set; }
    public TipoOperacao Tipo { get; private set; }
    public Guid ContaDebitoId { get; private set; }
    public Guid ContaCreditoId { get; private set; }
    public decimal Valor { get; private set; }
    public DateTime DataUtc { get; private set; }
    public IReadOnlyCollection<Lancamento> Lancamentos => _lancamentos.AsReadOnly();

    internal static OperacaoFinanceira Criar(
        TipoOperacao tipo, Conta contaDebito, Conta contaCredito, decimal valor)
    {
        Dinheiro.Validar(valor);
        if (contaDebito.Id == contaCredito.Id)
        {
            throw new ErroFinanceiroException(TipoErroFinanceiro.Validacao,
                "As contas de origem e destino devem ser diferentes.");
        }

        if (contaCredito.EhCompensacao ||
            (tipo == TipoOperacao.Transferencia && contaDebito.EhCompensacao) ||
            (tipo == TipoOperacao.DepositoSimulado && !contaDebito.EhCompensacao) ||
            tipo is not (TipoOperacao.DepositoSimulado or TipoOperacao.Transferencia))
        {
            throw new InvalidOperationException("Contas incompatíveis com o tipo da operação.");
        }

        var operacao = new OperacaoFinanceira
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            ContaDebitoId = contaDebito.Id,
            ContaCreditoId = contaCredito.Id,
            Valor = valor,
            DataUtc = DateTime.UtcNow
        };

        var debito = new Lancamento(operacao.Id, contaDebito.Id, NaturezaLancamento.Debito, valor);
        var credito = new Lancamento(operacao.Id, contaCredito.Id, NaturezaLancamento.Credito, valor);
        contaDebito.Aplicar(debito);
        contaCredito.Aplicar(credito);
        operacao._lancamentos.AddRange([debito, credito]);
        return operacao;
    }
}
