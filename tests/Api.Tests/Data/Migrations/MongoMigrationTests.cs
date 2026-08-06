using AwesomeAssertions;
using Defra.WasteObligations.Api.Data.Migrations;
using MigrationVersion = AdaskoTheBeAsT.MongoDbMigrations.Abstractions.Version;

namespace Defra.WasteObligations.Api.Tests.Data.Migrations;

public class MongoMigrationTests
{
    [Fact]
    public void Migrations_ShouldHaveSequentialVersionsAndNames()
    {
        var migrations = typeof(MongoMigration)
            .Assembly.GetTypes()
            .Where(x => !x.IsAbstract && x.IsAssignableTo(typeof(MongoMigration)))
            .Select(x =>
                Activator.CreateInstance(x) as MongoMigration
                ?? throw new InvalidOperationException($"Could not instantiate migration {x.Name}")
            )
            .OrderBy(x => x.Version.Major)
            .ThenBy(x => x.Version.Minor)
            .ThenBy(x => x.Version.Revision)
            .ToArray();

        migrations.Should().NotBeEmpty();

        for (var index = 0; index < migrations.Length; index++)
        {
            migrations[index].Version.Should().Be(new MigrationVersion(1, 0, index));
            migrations[index].Name.Should().StartWith($"{index + 1:000} - ");
        }
    }
}
