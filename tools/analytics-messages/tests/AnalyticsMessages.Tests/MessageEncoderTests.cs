using System.IO.Compression;
using System.Text;
using AwesomeAssertions;
using Xunit;

namespace Defra.WasteObligations.Tools.AnalyticsMessages.Tests;

public class MessageEncoderTests
{
    [Theory]
    [InlineData(258047)]
    [InlineData(258048)]
    public void Encode_WhenWithinBodyBudget_ShouldKeepOriginalText(int byteCount)
    {
        var input = new string('a', byteCount);

        var message = MessageEncoder.Encode(input);

        message.Body.Should().Be(input);
        message.MessageAttributes.Should().ContainSingle();
        message.MessageAttributes["Content-Type"].Should().Be(new MessageAttribute("String", "application/json"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Encode_WhenCompressed_ShouldPreserveUtf8Text(bool forceCompression)
    {
        var input = forceCompression ? "{\"value\":\"你好 café 😀\"}" : new string('é', 129025);

        var message = MessageEncoder.Encode(input, forceCompression);

        message.MessageAttributes.Should().HaveCount(2);
        message.MessageAttributes["Content-Type"].Should().Be(new MessageAttribute("String", "application/json"));
        message.MessageAttributes["Content-Encoding"].Should().Be(new MessageAttribute("String", "gzip+base64"));
        Encoding.UTF8.GetByteCount(message.Body).Should().BeLessThanOrEqualTo(258048);
        using var inputStream = new MemoryStream(Convert.FromBase64String(message.Body));
        using var gzip = new GZipStream(inputStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        reader.ReadToEnd().Should().Be(input);
    }

    [Fact]
    public void Encode_WhenOneByteAboveBodyBudget_ShouldCompress()
    {
        var message = MessageEncoder.Encode(new string('a', 258049));

        message.MessageAttributes.Should().ContainKey("Content-Encoding");
    }

    [Fact]
    public void Encode_WhenCompressedBodyStillExceedsBudget_ShouldReject()
    {
        var bytes = new byte[400000];
        new Random(123).NextBytes(bytes);
        var input = Convert.ToBase64String(bytes);

        var act = () => MessageEncoder.Encode(input);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Analytics event message exceeds the SNS message size limit.");
    }
}
