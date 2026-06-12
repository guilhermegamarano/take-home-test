namespace Fundo.Infrastructure.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class SqlServerTestGroup : ICollectionFixture<DockerSqlServerFixture>
{
    public const string Name = "SQL Server";
}
