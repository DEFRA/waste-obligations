using System.IO.Compression;
using System.Text;

namespace Defra.WasteObligations.AuditEvents.Analytics;

/// <summary>
/// The analytics message body and content headers shared by the SNS and SQS senders. A body larger than the
/// destination's budget is gzip-compressed and base64-encoded.
/// </summary>
internal sealed record AnalyticsMessage(string Body, string? ContentEncoding)
{
    public const string ContentTypeHeader = "Content-Type";
    public const string ContentEncodingHeader = "Content-Encoding";
    public const string JsonContentType = "application/json";
    public const string StringDataType = "String";

    private const string GzipBase64Encoding = "gzip+base64";

    // Leaves room for message attributes within each service's total message size limit.
    private const int MessageSizeBufferBytes = 4 * 1024;

    public const int SnsMaxBodyBytes = (256 * 1024) - MessageSizeBufferBytes;
    public const int SqsMaxBodyBytes = (1024 * 1024) - MessageSizeBufferBytes;

    public static AnalyticsMessage Create(string serializedMessage, int maxBodyBytes, string destination)
    {
        if (Encoding.UTF8.GetByteCount(serializedMessage) <= maxBodyBytes)
            return new AnalyticsMessage(serializedMessage, null);

        var compressedMessage = Compress(serializedMessage);

        if (Encoding.UTF8.GetByteCount(compressedMessage) > maxBodyBytes)
            throw new InvalidOperationException(
                $"Analytics event message exceeds the {destination} message size limit."
            );

        return new AnalyticsMessage(compressedMessage, GzipBase64Encoding);
    }

    private static string Compress(string serializedMessage)
    {
        var bytes = Encoding.UTF8.GetBytes(serializedMessage);
        using var output = new MemoryStream();

        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize))
        {
            gzip.Write(bytes);
        }

        return Convert.ToBase64String(output.ToArray());
    }
}
