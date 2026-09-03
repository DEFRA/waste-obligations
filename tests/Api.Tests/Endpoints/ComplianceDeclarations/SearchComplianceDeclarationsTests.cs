using System.Diagnostics.CodeAnalysis;
using System.Net;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteObligations.Api.Data;
using Defra.WasteObligations.Api.Dtos;
using Defra.WasteObligations.Api.Extensions;
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

    [Fact]
    public async Task Validation_WhenCountryUnknown_ShouldBeBadRequest()
    {
        var content = await RequestShouldBeBadRequest(EndpointQuery.New.Where(EndpointFilter.Country("GB-XXX")));

        await VerifyJson(content);
    }

    [Fact]
    public async Task Validation_WhenSortFieldUnknown_ShouldBeBadRequest()
    {
        var content = await RequestShouldBeBadRequest(EndpointQuery.New.Where(EndpointFilter.Sort("Unknown[asc]")));

        await VerifyJson(content);
    }

    [Theory]
    [InlineData("DateSubmitted[ascending]")]
    [InlineData("DateSubmitted[asc],DateSubmitted[desc]")]
    [InlineData("DateSubmitted[asc],")]
    public async Task Validation_WhenSortInvalid_ShouldBeBadRequest(string sort)
    {
        await RequestShouldBeBadRequest(EndpointQuery.New.Where(EndpointFilter.Sort(sort)));
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
    [SuppressMessage("ReSharper", "CSharp14OverloadResolutionWithSpanBreakingChange")]
    public async Task WhenValid_ShouldBeOk()
    {
        var client = CreateClient(testUser: TestUser.ReadOnly);
        ComplianceDeclarationService
            .Search(
                Arg.Is<ComplianceDeclarationSearchQuery>(x =>
                    x.ObligationYear == 2026
                    && x.Status.SequenceEqual(
                        new[]
                        {
                            Api.Data.Entities.ComplianceDeclarationStatus.Submitted,
                            Api.Data.Entities.ComplianceDeclarationStatus.Accepted,
                        }
                    )
                    && x.RegistrationType.SequenceEqual(
                        new[]
                        {
                            Api.Data.Entities.RegistrationType.DirectProducer,
                            Api.Data.Entities.RegistrationType.ComplianceScheme,
                        }
                    )
                    && x.BusinessCountry == BusinessCountryFilter.Wales.ToJsonValue()
                    && x.Search == "zeina"
                    && x.Sort != null
                    && x.Sort.Length == 0
                ),
                page: 1,
                pageSize: 20,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(
                new ComplianceDeclarationPageResult
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
                    .Where(EndpointFilter.Country(BusinessCountryFilter.Wales))
                    .Where(EndpointFilter.Search("zeina"))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenSortSpecified_ShouldMapSort()
    {
        ComplianceDeclarationService
            .Search(
                Arg.Is<ComplianceDeclarationSearchQuery>(x =>
                    x.Sort != null
                    && x.Sort.Length == 2
                    && x.Sort[0].Field == ComplianceDeclarationSortField.DateSubmitted
                    && x.Sort[0].Direction == ComplianceDeclarationSortDirection.Descending
                    && x.Sort[1].Field == ComplianceDeclarationSortField.OrganisationName
                    && x.Sort[1].Direction == ComplianceDeclarationSortDirection.Ascending
                ),
                page: 1,
                pageSize: 20,
                cancellationToken: Arg.Any<CancellationToken>()
            )
            .Returns(new ComplianceDeclarationPageResult());
        var client = CreateClient(testUser: TestUser.ReadOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.ComplianceDeclarations.Search(
                EndpointQuery.New.Where(EndpointFilter.Sort("DateSubmitted[desc],OrganisationName[asc]"))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
