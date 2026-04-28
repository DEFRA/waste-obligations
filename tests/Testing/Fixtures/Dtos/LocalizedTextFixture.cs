using AutoFixture;
using AutoFixture.Dsl;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Testing.Extensions;

namespace Defra.WasteObligations.Testing.Fixtures.Dtos;

public static class LocalizedTextFixture
{
    private static Fixture GetFixture() => new();

    private static readonly string[] s_languages = ["en", "cy"];

    public static IPostprocessComposer<LocalizedText> AddDefaults(this ICustomizationComposer<LocalizedText> composer)
    {
        return composer.With(x => x.Language, () => s_languages.Random());
    }

    public static IPostprocessComposer<LocalizedText> Text()
    {
        return GetFixture().Build<LocalizedText>().AddDefaults();
    }

    public static IPostprocessComposer<LocalizedText> Default()
    {
        return Text().With(x => x.Language, "en").With(x => x.Text, "This is the text");
    }
}
