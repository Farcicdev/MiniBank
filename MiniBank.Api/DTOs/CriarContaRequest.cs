using System.ComponentModel.DataAnnotations;

namespace MiniBank.Api.DTOs;

public record CriarContaRequest(
    [Required(ErrorMessage = "O número da conta é obrigatório.")] string Numero);
