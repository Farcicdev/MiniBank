using MiniBank.Api.DTOs;
using MiniBank.Api.Models;

namespace MiniBank.Api.Mappers;

public static class ContaMapper
{
    public static ContaResponse ToResponse(Conta conta)
    {
        return new ContaResponse(conta.Id, conta.Numero, conta.Saldo);
    }
}
