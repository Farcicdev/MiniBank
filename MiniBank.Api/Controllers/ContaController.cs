using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.Api.DTOs;
using MiniBank.Api.Mappers;
using MiniBank.Api.Service;
using Npgsql;

namespace MiniBank.Api.Controllers;

[ApiController]
[Route("api/contas")]
public class ContaController : ControllerBase
{
    private readonly ContaService _service;

    public ContaController(ContaService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<ContaResponse>> CriarConta(CriarContaRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var novaConta = await _service.CriarContaAsync(request.Numero, cancellationToken);

            return CreatedAtAction(nameof(ObterContaPorId), new { id = novaConta.Id },
                ContaMapper.ToResponse(novaConta));
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Conflito ao criar conta.",
                detail: "Já existe uma conta com os dados informados.");
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<ContaResponse>>> ListarContas(CancellationToken cancellationToken)
    {
        var contas = await _service.ListarContasAsync(cancellationToken);

        return Ok(contas.Select(ContaMapper.ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContaResponse>> ObterContaPorId(
        Guid id, CancellationToken cancellationToken)
    {
        var conta = await _service.ObterContaPorIdAsync(id, cancellationToken);

        if (conta is null)
        {
            return NotFound();
        }

        return Ok(ContaMapper.ToResponse(conta));
    }
}
