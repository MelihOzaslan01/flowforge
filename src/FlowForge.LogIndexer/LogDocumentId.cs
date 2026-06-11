using System.Security.Cryptography;
using System.Text;

namespace FlowForge.LogIndexer;

public static class LogDocumentId
{
    public static string Create(
        Guid runId,
        int stepNo,
        string eventType,
        int attempt,
        DateTimeOffset timestamp)
    {
        var input = string.Join(
            "|",
            runId.ToString("D"),
            stepNo.ToString(System.Globalization.CultureInfo.InvariantCulture),
            eventType,
            attempt.ToString(System.Globalization.CultureInfo.InvariantCulture),
            timestamp.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
