using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using EMG.Api.Modules.Projects.Dtos;
using EMG.Api.Modules.FinalAccounts.Services;
using EMG.Api.Modules.Projects.Repositories;
using EMG.Api.Modules.Projects.Services;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

internal static class ProjectCardMeasurements
{
    public static async Task RunAsync()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__EmgDb"));
        if (settings.Host != "127.0.0.1" || settings.Port != 55464 || settings.Database != "emg_restore")
            throw new InvalidOperationException("Expected the authorized loopback restore database.");
        settings.Options = "-c default_transaction_read_only=on";
        var sqlCount = 0;
        var options = new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(settings.ConnectionString)
            .LogTo(_ => sqlCount++, new[] { RelationalEventId.CommandExecuted }).Options;
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(type => {
            if (type.Type == typeof(ProjectSummary) && type.Properties.SingleOrDefault(p => p.Name == "Currencies") is { } added)
                type.Properties.Remove(added);
        });
        var json = new JsonSerializerOptions { TypeInfoResolver = resolver, Converters = { new MoneyConverter() } };
        for (var run = 0; run < 4; run++)
        {
            await using var db = new EmgDbContext(options);
            var projects = new ProjectsService(new EfProjectsRepository(db), new FinalAccountsService(db));
            sqlCount = 0;
            var timer = Stopwatch.StartNew();
            var cards = await projects.GetProjectsAsync(null, true, default);
            timer.Stop();
            var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(cards, json)));
            Console.WriteLine($"{(run == 0 ? "cold" : "warm")} {run}: {timer.ElapsedMilliseconds} ms; {sqlCount} SQL; {cards.Count} cards; {digest}");
        }
    }

    private sealed class MoneyConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetDecimal();
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteRawValue(value.ToString("G29", CultureInfo.InvariantCulture));
    }
}