using System.Net;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using EntityRegistrationType = Defra.WasteObligations.Api.Data.Entities.RegistrationType;

namespace Defra.WasteObligations.Api.Tests.Endpoints.ComplianceDeclarations;

public class SearchUnsubmittedComplianceDeclarationsTests(
    ApiWebApplicationFactory factory,
    ITestOutputHelper outputHelper
) : EndpointTestBase(factory, outputHelper)
{
    private IUnsubmittedOrganisationsService UnsubmittedOrganisationsService { get; } =
        Substitute.For<IUnsubmittedOrganisationsService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IUnsubmittedOrganisationsService>(_ => UnsubmittedOrganisationsService);
    }

    [Fact]
    public async Task WhenValid_ShouldMapRequestAndReturnUnsubmittedDeclarations()
    {
        var organisationId = Guid.Parse("c961459a-324c-4400-bb22-afae8c8a9827");
        UnsubmittedOrganisationsService
            .Search(
                2026,
                EntityRegistrationType.DirectProducer,
                Arg.Is<IReadOnlyCollection<ComplianceDeclarationSort>>(x =>
                    x.Single().Field == ComplianceDeclarationSortField.OrganisationName
                    && x.Single().Direction == ComplianceDeclarationSortDirection.Descending
                ),
                page: 2,
                pageSize: 5,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(
                new UnsubmittedOrganisationSearchResult
                {
                    Rows =
                    [
                        new UnsubmittedOrganisationSearchRow
                        {
                            OrganisationId = organisationId,
                            ObligationYear = 2026,
                            RegistrationType = EntityRegistrationType.DirectProducer,
                            Name = "Alpha Packaging",
                            ReferenceNumber = "100001",
                        },
                    ],
                    Total = 6,
                }
            );
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Unsubmitted(
                EndpointQuery
                    .New.Where(EndpointFilter.ObligationYear(2026))
                    .Where(EndpointFilter.RegistrationType("DirectProducer"))
                    .Where(EndpointFilter.Sort("OrganisationName[desc]"))
                    .Where(EndpointFilter.Page(2))
                    .Where(EndpointFilter.PageSize(5))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenValidForComplianceScheme_ShouldUseDefaultsAndReturnResponseShape()
    {
        var organisationId = Guid.Parse("b4d5584e-fa55-431f-9fcc-1bf747e001e4");
        UnsubmittedOrganisationsService
            .Search(
                2026,
                EntityRegistrationType.ComplianceScheme,
                Arg.Is<IReadOnlyCollection<ComplianceDeclarationSort>>(x => x.Count == 0),
                page: 1,
                pageSize: 20,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(
                new UnsubmittedOrganisationSearchResult
                {
                    Rows =
                    [
                        new UnsubmittedOrganisationSearchRow
                        {
                            OrganisationId = organisationId,
                            ObligationYear = 2026,
                            RegistrationType = EntityRegistrationType.ComplianceScheme,
                            Name = "Bravo Scheme",
                            ReferenceNumber = "200001",
                        },
                    ],
                    Total = 1,
                }
            );
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Unsubmitted(
                EndpointQuery
                    .New.Where(EndpointFilter.ObligationYear(2026))
                    .Where(EndpointFilter.RegistrationType("ComplianceScheme"))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenWriteOnlyUser_ShouldBeForbidden()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Unsubmitted(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(2022)]
    [InlineData(2051)]
    public async Task Validation_WhenObligationYearInvalid_ShouldBeBadRequest(int obligationYear)
    {
        var content = await RequestShouldBeBadRequest(
            EndpointQuery
                .New.Where(EndpointFilter.ObligationYear(obligationYear))
                .Where(EndpointFilter.RegistrationType("DirectProducer"))
        );

        await VerifyJson(content);
    }

    [Fact]
    public async Task Validation_WhenRegistrationTypeUnknown_ShouldBeBadRequest()
    {
        var content = await RequestShouldBeBadRequest(
            EndpointQuery
                .New.Where(EndpointFilter.ObligationYear(2026))
                .Where(EndpointFilter.RegistrationType("Unknown"))
        );

        await VerifyJson(content);
    }

    [Theory]
    [InlineData("Unknown[asc]")]
    [InlineData("OrganisationName[ascending]")]
    [InlineData("OrganisationName[asc],OrganisationName[desc]")]
    public async Task Validation_WhenSortInvalid_ShouldBeBadRequest(string sort)
    {
        var content = await RequestShouldBeBadRequest(
            EndpointQuery
                .New.Where(EndpointFilter.ObligationYear(2026))
                .Where(EndpointFilter.RegistrationType("DirectProducer"))
                .Where(EndpointFilter.Sort(sort))
        );

        await VerifyJson(content);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Validation_WhenPageSizeInvalid_ShouldBeBadRequest(int pageSize)
    {
        var content = await RequestShouldBeBadRequest(
            EndpointQuery
                .New.Where(EndpointFilter.ObligationYear(2026))
                .Where(EndpointFilter.RegistrationType("DirectProducer"))
                .Where(EndpointFilter.PageSize(pageSize))
        );

        await VerifyJson(content);
    }

    [Fact]
    public async Task WhenSortIsUnsupported_ShouldBeBadRequest()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Unsubmitted(
                EndpointQuery
                    .New.Where(EndpointFilter.ObligationYear(2026))
                    .Where(EndpointFilter.RegistrationType("DirectProducer"))
                    .Where(EndpointFilter.Sort("PercentageMet[asc]"))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await UnsubmittedOrganisationsService
            .DidNotReceive()
            .Search(
                Arg.Any<int>(),
                Arg.Any<EntityRegistrationType>(),
                Arg.Any<IReadOnlyCollection<ComplianceDeclarationSort>>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task WhenEligibilityDataIsUnavailable_ShouldBeServiceUnavailable()
    {
        UnsubmittedOrganisationsService
            .Search(
                Arg.Any<int>(),
                Arg.Any<EntityRegistrationType>(),
                Arg.Any<IReadOnlyCollection<ComplianceDeclarationSort>>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromException<UnsubmittedOrganisationSearchResult>(
                    new UnsubmittedOrganisationsUnavailableException("Organisation eligibility data is unavailable")
                )
            );
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Unsubmitted(
                EndpointQuery
                    .New.Where(EndpointFilter.ObligationYear(2026))
                    .Where(EndpointFilter.RegistrationType("DirectProducer"))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<string> RequestShouldBeBadRequest(EndpointQuery query)
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Unsubmitted(query),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
