using System.Security.Cryptography;
using System.Text;

namespace MiniBank.Api.Rules;

public class IdempotenciaHash
{
    public static string Gerar(Guid Origem, Guid destino, decimal valor)
    {
        var conteudo = FormattableString.Invariant($"transferencia:v1;{Origem:D}:{destino:D}:{valor:F2}");

        var bytes = Encoding.UTF8.GetBytes(conteudo);

        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }
}