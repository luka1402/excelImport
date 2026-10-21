using ExcelImporter.Application.Rules;
using ExcelImporter.Application.Schema;
using ExcelImporter.Domain.Rules;
using NSubstitute;
using Shouldly;

namespace ExcelImporter.Application.Tests;

public class LookupDictionaryLoaderTests
{
    private const string ConnectionString = "cs";
    private readonly ISchemaProvider schemaProvider = Substitute.For<ISchemaProvider>();
    private readonly ILookupDataSource dataSource = Substitute.For<ILookupDataSource>();

    public LookupDictionaryLoaderTests()
    {
        var gradovi = new TableSchema(
            "dbo",
            "Gradovi",
            [new("Id", 1, "int", false, null, 10, 0, true, false), new("Naziv", 2, "nvarchar", false, 100, 0, 0, false, false)],
            ["Id"],
            []);
        schemaProvider.GetSchemaAsync(ConnectionString, Arg.Any<CancellationToken>()).Returns(new DatabaseSchema([gradovi]));
    }

    private static LookupRule RuleFor(string table = "Gradovi", string key = "Naziv", string value = "Id") =>
        new() { TargetField = "GradId", SourceField = "Grad", Table = table, KeyColumn = key, ReturnColumn = value };

    private LookupDictionaryLoader Loader() => new(schemaProvider, dataSource);

    [Fact]
    public async Task Loads_dictionary_using_canonical_names_from_schema()
    {
        dataSource.ReadEntriesAsync(ConnectionString, "dbo", "Gradovi", "Naziv", "Id", Arg.Any<CancellationToken>())
            .Returns([new KeyValuePair<string, object?>("Novi Sad", 2)]);

        var dictionary = await Loader().LoadAsync(ConnectionString, RuleFor("gradovi", "NAZIV", "id"), TestContext.Current.CancellationToken);

        dictionary.TryGetValue("novi sad", out var id).ShouldBeTrue();
        id.ShouldBe(2);
    }

    [Theory]
    [InlineData("Nepostojeca", "Naziv", "Id")]
    [InlineData("Gradovi", "Nepostojeca", "Id")]
    [InlineData("Gradovi", "Naziv", "Nepostojeca")]
    [InlineData("Gradovi]; DROP TABLE x;--", "Naziv", "Id")]
    public async Task Unknown_table_or_column_is_rejected_without_touching_data(string table, string key, string value)
    {
        await Should.ThrowAsync<LookupConfigurationException>(
            () => Loader().LoadAsync(ConnectionString, RuleFor(table, key, value), TestContext.Current.CancellationToken));

        await dataSource.DidNotReceiveWithAnyArgs().ReadEntriesAsync(default!, default!, default!, default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Duplicate_key_in_table_is_configuration_error()
    {
        dataSource.ReadEntriesAsync(default!, default!, default!, default!, default!, TestContext.Current.CancellationToken)
            .ReturnsForAnyArgs([new KeyValuePair<string, object?>("Beograd", 1), new KeyValuePair<string, object?>("beograd ", 2)]);

        var ex = await Should.ThrowAsync<LookupConfigurationException>(
            () => Loader().LoadAsync(ConnectionString, RuleFor(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("beograd");
    }
}
