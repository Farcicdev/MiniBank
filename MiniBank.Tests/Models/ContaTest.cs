using JetBrains.Annotations;
using MiniBank.Api.Models;

namespace MiniBank.Tests.Models;

[TestSubject(typeof(Conta))]
public class ContaTest
{

    [Fact]
    public void NovaConta_DeveTerSaldoZero()
    {
        // Arrange
        var conta = new Conta();

        // Assert
        Assert.Equal(0m, conta.Saldo);
    }
}