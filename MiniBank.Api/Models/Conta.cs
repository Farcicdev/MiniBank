namespace MiniBank.Api.Models;

public class Conta
{
    public Guid Id { get; set; }

    public string Numero { get; set; } = string.Empty;
    
    public decimal Saldo { get; private set; } 
}