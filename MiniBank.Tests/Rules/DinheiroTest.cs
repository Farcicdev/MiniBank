using JetBrains.Annotations;
using MiniBank.Api.Models;
using MiniBank.Api.Rules;

namespace MiniBank.Tests.Rules;

[TestSubject(typeof(Dinheiro))]
public class DinheiroTest
{

    [Fact]
    public void Validar_ValorNegativo_DeveLancarExcecao()
    {
        // Arrange
        decimal valor = -50m;

        // Act + Assert
        Assert.Throws<ErroFinanceiroException>(
            () => Dinheiro.Validar(valor)
        );
    }
    
    [Theory]
    [InlineData(-50)]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(10.123)]
    public void Validar_ValorInvalido_DeveLancarExcecao(decimal valor)
    {
        Assert.Throws<ErroFinanceiroException>(
            () => Dinheiro.Validar(valor)
        );
    }

    [Fact]
    public void Validar_Valor()
    {
        decimal valor = 100m;

        Dinheiro.Validar(valor);
    }
    
    [Fact]
    public void Validar_ValorNegativo_DeveRetornarErroValidacao()
    {
        // Arrange
        decimal valor = -50m;

        // Act
        var exception = Assert.Throws<ErroFinanceiroException>(
            () => Dinheiro.Validar(valor)
        );

        // Assert
        Assert.Equal(TipoErroFinanceiro.Validacao, exception.Tipo);
    }

}