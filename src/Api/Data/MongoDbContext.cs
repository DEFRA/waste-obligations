using System.Diagnostics.CodeAnalysis;

namespace Defra.WasteObligations.Api.Data;

[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public class MongoDbContext : IDbContext;
