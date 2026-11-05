using MiniBank.Api.Models;

namespace MiniBank.Api.Rules;

public static class Dinheiro
{
    public const decimal ValorMaximo = 9999999999999999.99m;

    public static void Validar(decimal valor)
    {
        if (valor <= 0 || valor > ValorMaximo || decimal.Round(valor, 2) != valor)
        {
            throw new ErroFinanceiroException(TipoErroFinanceiro.Validacao,
                "O valor deve ser positivo, ter no máximo duas casas decimais e não exceder 9999999999999999,99.");
        }
    }
}
