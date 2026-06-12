using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace Fundo.Infrastructure.Tests.Infrastructure;

public sealed class DockerSqlServerFixture : IAsyncLifetime
{
    private const string Password = "SqlOnlyPassword!12345";
    private readonly string containerName = $"fundo-loans-tests-{Guid.NewGuid():N}";

    public string MasterConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await RunDockerAsync(
            "run",
            "--detach",
            "--rm",
            "--name",
            containerName,
            "-e",
            "ACCEPT_EULA=Y",
            "-e",
            $"MSSQL_SA_PASSWORD={Password}",
            "-p",
            "127.0.0.1::1433",
            "mcr.microsoft.com/mssql/server:2022-latest");

        var endpoint = (await RunDockerAsync("port", containerName, "1433/tcp")).Trim();
        var port = endpoint[(endpoint.LastIndexOf(':') + 1)..];
        MasterConnectionString = new SqlConnectionStringBuilder
        {
            DataSource = $"127.0.0.1,{port}",
            UserID = "sa",
            Password = Password,
            InitialCatalog = "master",
            Encrypt = true,
            TrustServerCertificate = true,
            ConnectTimeout = 5,
        }.ConnectionString;

        await WaitUntilAvailableAsync();
    }

    public async Task DisposeAsync()
    {
        await RunDockerAsync("rm", "--force", containerName);
    }

    public string CreateDatabaseConnectionString(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(MasterConnectionString)
        {
            InitialCatalog = databaseName,
        };

        return builder.ConnectionString;
    }

    private async Task WaitUntilAvailableAsync()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                await using var connection = new SqlConnection(MasterConnectionString);
                await connection.OpenAsync(cancellation.Token);
                return;
            }
            catch (SqlException)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellation.Token);
            }
        }
    }

    private static async Task<string> RunDockerAsync(params string[] arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = "docker";
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.UseShellExecute = false;

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Docker command failed with exit code {process.ExitCode}: {error}");
        }

        return output;
    }
}
