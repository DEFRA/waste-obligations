using AwesomeAssertions;
using Defra.WasteObligations.Api.Schemas;

namespace Defra.WasteObligations.Api.Tests.Schemas;

public class EmbeddedEntityJsonSchemaProviderTests
{
    [Fact]
    public void Get_WhenSchemaCannotBeFound_ShouldThrow()
    {
        var subject = new EmbeddedEntityJsonSchemaProvider();

        var act = () => subject.Get("unknown_entity", "v1.0");

        act.Should()
            .Throw<FileNotFoundException>()
            .WithMessage("Could not find embedded entity schema 'unknown-entity.v1.0.schema.json'.");
    }
}
