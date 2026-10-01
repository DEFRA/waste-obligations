using System.IO.Compression;
using System.Text;

namespace Defra.WasteObligations.Tools.AnalyticsMessages;

public static class MessageEncoder
{
    // Keep this transport contract aligned with SnsAnalyticsEventSender.
    public const int MaxMessageBodyBytes = (256 * 1024) - (4 * 1024);

    public static EncodedMessage Encode(string serializedMessage, bool forceCompression = false)
    {
        var attributes = new Dictionary<string, MessageAttribute>
        {
            ["Content-Type"] = new("String", "application/json"),
        };

        if (!forceCompression && Encoding.UTF8.GetByteCount(serializedMessage) <= MaxMessageBodyBytes)
        {
            return new EncodedMessage(serializedMessage, attributes);
        }

        var bytes = Encoding.UTF8.GetBytes(serializedMessage);
        using var output = new MemoryStream();

        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize))
        {
            gzip.Write(bytes);
        }

        var body = Convert.ToBase64String(output.ToArray());

        if (Encoding.UTF8.GetByteCount(body) > MaxMessageBodyBytes)
        {
            throw new InvalidOperationException("Analytics event message exceeds the SNS message size limit.");
        }

        attributes["Content-Encoding"] = new("String", "gzip+base64");

        return new EncodedMessage(body, attributes);
    }
}
