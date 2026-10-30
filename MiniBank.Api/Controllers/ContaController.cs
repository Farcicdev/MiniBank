using Microsoft.AspNetCore.Mvc;
using MiniBank.Api.Models;
using MiniBank.Api.Service;

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
    public async Task<ActionResult<Conta>> CriarConta(Conta conta)
    {
        var novaConta = await _service.CriarContaAsync(conta);

        return Created($"api/contas/{novaConta.Id}", novaConta);
    }
}