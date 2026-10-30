using MiniBank.Api.Data;
using MiniBank.Api.Models;

namespace MiniBank.Api.Service;

public class ContaService
{
    private readonly MiniBankDbContext _context;

    public ContaService(MiniBankDbContext context)
    {
        _context = context;
    }

    public async Task<Conta> CriarContaAsync(Conta conta)
    {
        var novaConta = new Conta()
        {
            Id = Guid.NewGuid(),
            Numero = conta.Numero
        };
        _context.Add(novaConta);

        await _context.SaveChangesAsync();

        return novaConta;
    } 
}