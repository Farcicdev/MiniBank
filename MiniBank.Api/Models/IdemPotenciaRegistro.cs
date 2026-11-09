namespace MiniBank.Api.Models;

public class IdemPotenciaRegistro
{
    public Guid Id { get; private set; }
    //Guardar chave da transferencia
    public string Chave { get; private set; } = string.Empty;
    //guardar conteudo da transferencia
    public string RequestHash { get; private set; } = string.Empty;
    
    public Guid OperacaoFinanceiraId { get; private set; }
}