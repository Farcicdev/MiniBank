using Microsoft.EntityFrameworkCore;
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

    public async Task<Conta> CriarContaAsync(string numero, CancellationToken cancellationToken = default)
    {
        var novaConta = new Conta
        {
            Id = Guid.NewGuid(),
            Numero = numero
        };
        _context.Contas.Add(novaConta);

        await _context.SaveChangesAsync(cancellationToken);

        return novaConta;
    }

    public async Task<List<Conta>> ListarContasAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Contas.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<Conta?> ObterContaPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Contas.AsNoTracking()
            .SingleOrDefaultAsync(conta => conta.Id == id, cancellationToken);
    }
}
