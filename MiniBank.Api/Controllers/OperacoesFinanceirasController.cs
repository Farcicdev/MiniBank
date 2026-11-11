using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniBank.Api.DTOs;
using MiniBank.Api.Mappers;
using MiniBank.Api.Models;
using MiniBank.Api.Service;
using Npgsql;

namespace MiniBank.Api.Controllers;

[ApiController]
public class OperacoesFinanceirasController(
    OperacaoFinanceiraService service, ILogger<OperacoesFinanceirasController> logger) : ControllerBase
{
    [HttpPost("api/contas/{id:guid}/depositos")]
    public Task<ActionResult<OperacaoFinanceiraResponse>> Depositar(
        Guid id, DepositoRequest request, CancellationToken cancellationToken) =>
        ExecutarAsync(() => service.DepositarAsync(id, request.Valor, cancellationToken));

    [HttpPost("api/transferencias")]
    public Task<ActionResult<OperacaoFinanceiraResponse>> Transferir(
        TransferenciaRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? chave,
        CancellationToken cancellationToken) =>
        ExecutarAsync(() => service.TransferirAsync(
            request.ContaOrigemId, 
            request.ContaDestinoId,
            request.Valor, 
            chave,
            cancellationToken));

    private async Task<ActionResult<OperacaoFinanceiraResponse>> ExecutarAsync(
        Func<Task<OperacaoFinanceira>> executar)
    {
        try
        {
            var operacao = await executar();
            return Ok(OperacaoFinanceiraMapper.ToResponse(operacao));
        }
        catch (ErroFinanceiroException exception)
        {
            var status = exception.Tipo switch
            {
                TipoErroFinanceiro.Validacao => StatusCodes.Status400BadRequest,
                TipoErroFinanceiro.ContaNaoEncontrada => StatusCodes.Status404NotFound,
                TipoErroFinanceiro.Conflito => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };
            return Problem(statusCode: status, title: "Operação financeira recusada.", detail: exception.Message);
        }
        catch (Exception exception) when (exception is DbUpdateException or NpgsqlException or InvalidOperationException)
        {
            logger.LogError(exception, "Falha ao processar operação financeira.");
            return Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "Não foi possível concluir a operação financeira.");
        }
    }
}
