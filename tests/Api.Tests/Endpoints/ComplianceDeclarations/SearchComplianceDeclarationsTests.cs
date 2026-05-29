using System.Net;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Services;
using Defra.WasteObligations.Testing;
using Defra.WasteObligations.Testing.Fixtures.Entities;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using NSubstitute;

namespace Defra.WasteObligations.Api.Tests.Endpoints.ComplianceDeclarations;

public class SearchComplianceDeclarationsTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IComplianceDeclarationService ComplianceDeclarationService { get; } =
        Substitute.For<IComplianceDeclarationService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddTransient<IComplianceDeclarationService>(_ => ComplianceDeclarationService);
    }

    [Fact]
    public async Task WhenWriteOnlyUser_ShouldBeForbidden()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Search(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(ObligationYear.Minimum - 1)]
    [InlineData(ObligationYear.Maximum + 1)]
    public async Task Validation_WhenObligationYearInvalid_ShouldBeBadRequest(int obligationYear)
    {
        var content = await RequestShouldBeBadRequest(
            EndpointQuery.New.Where(EndpointFilter.ObligationYear(obligationYear))
        );

        await VerifyJson(content);
    }

    [Fact]
    public async Task Validation_WhenStatusUnknown_ShouldBeBadRequest()
    {
        var content = await RequestShouldBeBadRequest(EndpointQuery.New.Where(EndpointFilter.Status("unknown")));

        await VerifyJson(content);
    }

    [Fact]
    public async Task Validation_WhenRegistrationTypeUnknown_ShouldBeBadRequest()
    {
        var content = await RequestShouldBeBadRequest(
            EndpointQuery.New.Where(EndpointFilter.RegistrationType("unknown"))
        );

        await VerifyJson(content);
    }

    [Theory]
    [InlineData(0)]
    public async Task Validation_WhenPageInvalid_ShouldBeBadRequest(int page)
    {
        var content = await RequestShouldBeBadRequest(EndpointQuery.New.Where(EndpointFilter.Page(page)));

        await VerifyJson(content);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Validation_WhenPageSizeInvalid_ShouldBeBadRequest(int pageSize)
    {
        var content = await RequestShouldBeBadRequest(EndpointQuery.New.Where(EndpointFilter.PageSize(pageSize)));

        await VerifyJson(content);
    }

    [Fact]
    public async Task WhenValid_ShouldBeOk()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);
        ComplianceDeclarationService
            .Search(
                obligationYear: 2026,
                status: Arg.Is<Api.Data.Entities.ComplianceDeclarationStatus[]?>(x =>
                    x != null
                    // ReSharper disable once CSharp14OverloadResolutionWithSpanBreakingChange
                    && x.SequenceEqual(
                        new[]
                        {
                            Api.Data.Entities.ComplianceDeclarationStatus.Submitted,
                            Api.Data.Entities.ComplianceDeclarationStatus.Accepted,
                        }
                    )
                ),
                registrationType: Arg.Is<Api.Data.Entities.RegistrationType[]?>(x =>
                    x != null
                    // ReSharper disable once CSharp14OverloadResolutionWithSpanBreakingChange
                    && x.SequenceEqual(
                        new[]
                        {
                            Api.Data.Entities.RegistrationType.DirectProducer,
                            Api.Data.Entities.RegistrationType.ComplianceScheme,
                        }
                    )
                ),
                organisationName: "org name",
                page: 1,
                pageSize: 20,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(
                new ComplianceDeclarationSearchResult
                {
                    ComplianceDeclarations =
                    [
                        ComplianceDeclarationFixture.DirectProducer().With(x => x.Id, ObjectId.Empty).Create(),
                    ],
                    Total = 1,
                }
            );

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Search(
                EndpointQuery
                    .New.Where(EndpointFilter.ObligationYear(2026))
                    .Where(
                        EndpointFilter.Status([
                            ComplianceDeclarationStatus.Submitted,
                            ComplianceDeclarationStatus.Accepted,
                        ])
                    )
                    .Where(
                        EndpointFilter.RegistrationType([
                            RegistrationType.DirectProducer,
                            RegistrationType.ComplianceScheme,
                        ])
                    )
                    .Where(EndpointFilter.OrganisationName("org name"))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<string> RequestShouldBeBadRequest(EndpointQuery query)
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Search(query),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
