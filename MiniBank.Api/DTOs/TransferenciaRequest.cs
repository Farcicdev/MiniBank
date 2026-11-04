namespace MiniBank.Api.DTOs;

public record TransferenciaRequest(Guid ContaOrigemId, Guid ContaDestinoId, decimal Valor);
