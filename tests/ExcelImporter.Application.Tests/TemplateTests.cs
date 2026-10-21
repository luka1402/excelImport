using ExcelImporter.Application.Mapping;
using ExcelImporter.Application.Schema;
using ExcelImporter.Application.Templates;
using ExcelImporter.Domain.Rules;
using Shouldly;

namespace ExcelImporter.Application.Tests;

public class TemplateTests
{
    private static ColumnSchema Column(string name, string dataType = "nvarchar", bool nullable = true, int? maxLength = 50, bool identity = false) =>
        new(name, 1, dataType, nullable, maxLength, 0, 0, identity, false);

    private static readonly DatabaseSchema _schema = new(
    [
        new TableSchema("dbo", "Customers",
        [
            Column("Id", "int", false, null, identity: true),
            Column("Name"),
            Column("City"),
            Column("CityId", "int", true, null),
            Column("Status", maxLength: 10),
        ], ["Id"], []),
        new TableSchema("dbo", "Cities", [Column("Id", "int", false, null), Column("Name")], ["Id"], []),
    ]);

    private static readonly string[] _fileColumns = ["Ime", "Grad", "Šifra"];

    private static TemplateContent Content() => new(
        _fileColumns,
        [new ColumnMapping("Ime", "Name"), new ColumnMapping("Grad", "City")],
        [new LookupRule { TargetField = "CityId", SourceField = "Grad", Table = "Cities", KeyColumn = "Name", ReturnColumn = "Id" }],
        [new ColumnConstant("Status", ConstantValue.FromLiteral("Aktivan"))]);

    private static TemplateDraft Draft(string name = "Kupci", TemplateContent? content = null) =>
        new(name, 1, "dbo", "Customers", "List1", 1, content ?? Content());

    private static TemplateService CreateService(out FakeStore store, DatabaseSchema? schema = null)
    {
        store = new FakeStore();
        return new TemplateService(store, new FakeSchemaSource(schema ?? _schema));
    }

    [Fact]
    public async Task SaveThenLoad_ReturnsIdenticalState()
    {
        var service = CreateService(out _);
        var content = Content();

        var created = await service.CreateAsync(Draft(content: content), TestContext.Current.CancellationToken);
        var loaded = await service.GetAsync(created.Id, TestContext.Current.CancellationToken);

        loaded.Name.ShouldBe("Kupci");
        loaded.SchemaName.ShouldBe("dbo");
        loaded.TableName.ShouldBe("Customers");
        loaded.SheetName.ShouldBe("List1");
        loaded.Content.SourceColumns.ShouldBe(content.SourceColumns);
        loaded.Content.Mappings.ShouldBe(content.Mappings);
        loaded.Content.Constants.ShouldBe(content.Constants);
        loaded.Content.Rules.ShouldBe(content.Rules);
        loaded.ColumnsFingerprint.ShouldBe(ColumnsFingerprint.Compute(_fileColumns));
    }

    [Fact]
    public void ContentSerializer_RoundTripsThroughJson()
    {
        var content = Content();

        var (mapping, rules, constants) = TemplateContentSerializer.Serialize(content);
        var restored = TemplateContentSerializer.Deserialize(mapping, rules, constants);

        restored.Mappings.ShouldBe(content.Mappings);
        restored.Rules.ShouldBe(content.Rules);
        restored.Constants.ShouldBe(content.Constants);
        restored.SourceColumns.ShouldBe(content.SourceColumns);
    }

    [Fact]
    public void ContentSerializer_CorruptedJson_ThrowsTemplateInvalid()
    {
        var (mapping, _, constants) = TemplateContentSerializer.Serialize(Content());

        Should.Throw<TemplateInvalidException>(() => TemplateContentSerializer.Deserialize(mapping, """[{"type":"nepoznato","version":1}]""", constants));
        Should.Throw<TemplateInvalidException>(() => TemplateContentSerializer.Deserialize("{", "[]", "[]"));
    }

    [Fact]
    public async Task Create_DuplicateName_IsRejected_CaseInsensitive()
    {
        var service = CreateService(out _);
        await service.CreateAsync(Draft("Kupci"), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<DuplicateTemplateNameException>(() => service.CreateAsync(Draft("  kupci "), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_ToNameOfAnotherTemplate_IsRejected_ButKeepingOwnNameIsAllowed()
    {
        var service = CreateService(out _);
        var first = await service.CreateAsync(Draft("A"), TestContext.Current.CancellationToken);
        await service.CreateAsync(Draft("B"), TestContext.Current.CancellationToken);

        await Should.ThrowAsync<DuplicateTemplateNameException>(() => service.UpdateAsync(first.Id, Draft("B"), TestContext.Current.CancellationToken));
        var updated = await service.UpdateAsync(first.Id, Draft("A"), TestContext.Current.CancellationToken);
        updated.Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task UpdateAndDelete_MissingTemplate_ThrowNotFound()
    {
        var service = CreateService(out _);

        await Should.ThrowAsync<TemplateNotFoundException>(() => service.UpdateAsync(99, Draft(), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<TemplateNotFoundException>(() => service.DeleteAsync(99, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<TemplateNotFoundException>(() => service.GetAsync(99, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delete_RemovesTemplateFromList()
    {
        var service = CreateService(out _);
        var created = await service.CreateAsync(Draft(), TestContext.Current.CancellationToken);

        await service.DeleteAsync(created.Id, TestContext.Current.CancellationToken);

        (await service.ListAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_NormalizesNameAndUsesCanonicalSchemaNames()
    {
        var service = CreateService(out _);
        var draft = Draft("  Kupci  ") with { SchemaName = "DBO", TableName = "customers" };

        var created = await service.CreateAsync(draft, TestContext.Current.CancellationToken);

        created.Name.ShouldBe("Kupci");
        created.SchemaName.ShouldBe("dbo");
        created.TableName.ShouldBe("Customers");
    }

    [Theory]
    [InlineData("Customers", "Nepostojeca", "Ime")]
    public async Task Create_MappingToMissingColumn_IsRejected(string table, string targetColumn, string sourceColumn)
    {
        var service = CreateService(out _);
        var content = Content() with { Mappings = [new ColumnMapping(sourceColumn, targetColumn)] };

        var exception = await Should.ThrowAsync<TemplateInvalidException>(
            () => service.CreateAsync(Draft(content: content) with { TableName = table }, TestContext.Current.CancellationToken));

        exception.Errors.ShouldContain(error => error.Contains("Nepostojeca"));
    }

    [Fact]
    public async Task Create_MissingTable_IsRejected()
    {
        var service = CreateService(out _);

        var exception = await Should.ThrowAsync<TemplateInvalidException>(
            () => service.CreateAsync(Draft() with { TableName = "Ne_postoji" }, TestContext.Current.CancellationToken));

        exception.Errors.ShouldContain(error => error.Contains("Ne_postoji"));
    }

    [Fact]
    public async Task Create_RuleReferencingMissingColumnOrLookupTable_IsRejected()
    {
        var service = CreateService(out _);
        var content = Content() with
        {
            Rules =
            [
                new DefaultValueRule { TargetField = "NemaMe", Value = "x" },
                new LookupRule { TargetField = "CityId", SourceField = "Grad", Table = "Drzave", KeyColumn = "Name", ReturnColumn = "Id" },
                new LookupRule { TargetField = "CityId", SourceField = "Grad", Table = "Cities", KeyColumn = "Naziv", ReturnColumn = "Id" },
            ],
        };

        var exception = await Should.ThrowAsync<TemplateInvalidException>(
            () => service.CreateAsync(Draft(content: content), TestContext.Current.CancellationToken));

        exception.Errors.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Create_ConstantOnMappedColumn_IsRejected()
    {
        var service = CreateService(out _);
        var content = Content() with { Constants = [new ColumnConstant("Name", ConstantValue.FromLiteral("x"))] };

        await Should.ThrowAsync<TemplateInvalidException>(() => service.CreateAsync(Draft(content: content), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_EmptyName_IsRejected(string name)
    {
        var service = CreateService(out _);

        await Should.ThrowAsync<TemplateInvalidException>(() => service.CreateAsync(Draft(name), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Apply_SameFile_HasNoWarning()
    {
        var service = CreateService(out _);
        var created = await service.CreateAsync(Draft(), TestContext.Current.CancellationToken);

        var applied = await service.ApplyAsync(created.Id, _fileColumns, TestContext.Current.CancellationToken);

        applied.HasColumnWarning.ShouldBeFalse();
        applied.IsUsable.ShouldBeTrue();
        applied.Mappings.ShouldBe(Content().Mappings);
    }

    [Fact]
    public async Task Apply_ReorderedAndDifferentlyCasedColumns_HasNoWarning()
    {
        var service = CreateService(out _);
        var created = await service.CreateAsync(Draft(), TestContext.Current.CancellationToken);

        var applied = await service.ApplyAsync(created.Id, ["ŠIFRA", "grad", "ime"], TestContext.Current.CancellationToken);

        applied.HasColumnWarning.ShouldBeFalse();
        applied.Mappings.ShouldBe([new ColumnMapping("ime", "Name"), new ColumnMapping("grad", "City")]);
    }

    [Fact]
    public async Task Apply_ChangedColumns_ListsNewAndMissingAndDropsUnmatchedMappings()
    {
        var service = CreateService(out _);
        var created = await service.CreateAsync(Draft(), TestContext.Current.CancellationToken);

        var applied = await service.ApplyAsync(created.Id, ["Ime", "Telefon"], TestContext.Current.CancellationToken);

        applied.HasColumnWarning.ShouldBeTrue();
        applied.Differences.NewColumns.ShouldBe(["Telefon"]);
        applied.Differences.MissingColumns.ShouldBe(["Grad", "Šifra"]);
        applied.Mappings.ShouldBe([new ColumnMapping("Ime", "Name")]);
    }

    [Fact]
    public async Task Apply_WhenTargetSchemaNoLongerHasTheColumn_MarksTemplateUnusable()
    {
        var service = CreateService(out var store);
        var created = await service.CreateAsync(Draft(), TestContext.Current.CancellationToken);
        var reduced = new DatabaseSchema(
        [
            new TableSchema("dbo", "Customers", [Column("Id", "int", false, null, identity: true), Column("Name"), Column("Status", maxLength: 10)], ["Id"], []),
            new TableSchema("dbo", "Cities", [Column("Id", "int", false, null), Column("Name")], ["Id"], []),
        ]);
        var applyingService = new TemplateService(store, new FakeSchemaSource(reduced));

        var applied = await applyingService.ApplyAsync(created.Id, _fileColumns, TestContext.Current.CancellationToken);

        applied.IsUsable.ShouldBeFalse();
        applied.SchemaIssues.ShouldContain(issue => issue.Contains("City"));
    }

    [Fact]
    public void Fingerprint_IgnoresOrderCaseAndSpacing_ButDetectsChanges()
    {
        var baseline = ColumnsFingerprint.Compute(["Ime", "Šifra Kupca"]);

        ColumnsFingerprint.Compute(["sifra_kupca", "IME"]).ShouldBe(baseline);
        ColumnsFingerprint.Compute(["Ime", "Šifra Kupca", "Grad"]).ShouldNotBe(baseline);
        ColumnsFingerprint.Compute(["Ime"]).ShouldNotBe(baseline);
        baseline.Length.ShouldBe(64);
    }

    private sealed class FakeSchemaSource(DatabaseSchema schema) : ITemplateSchemaSource
    {
        public Task<DatabaseSchema> GetSchemaAsync(int connectionId, CancellationToken cancellationToken) => Task.FromResult(schema);
    }

    private sealed class FakeStore : ITemplateStore
    {
        private readonly Dictionary<int, SavedTemplate> _items = [];
        private int _nextId = 1;

        public Task<IReadOnlyList<TemplateSummary>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TemplateSummary>>([.. _items.Values.Select(t => new TemplateSummary(t.Id, t.Name, t.ConnectionId, t.SchemaName, t.TableName, t.UpdatedAt))]);

        public Task<SavedTemplate?> FindAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(_items.GetValueOrDefault(id));

        public Task<SavedTemplate> AddAsync(TemplateDraft draft, string columnsFingerprint, CancellationToken cancellationToken)
        {
            EnsureNameIsFree(draft.Name, null);
            var saved = ToSaved(_nextId++, draft, columnsFingerprint);
            _items[saved.Id] = saved;
            return Task.FromResult(saved);
        }

        public Task<SavedTemplate?> UpdateAsync(int id, TemplateDraft draft, string columnsFingerprint, CancellationToken cancellationToken)
        {
            if (!_items.ContainsKey(id))
            {
                return Task.FromResult<SavedTemplate?>(null);
            }

            EnsureNameIsFree(draft.Name, id);
            return Task.FromResult<SavedTemplate?>(_items[id] = ToSaved(id, draft, columnsFingerprint));
        }

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) => Task.FromResult(_items.Remove(id));

        private void EnsureNameIsFree(string name, int? excludedId)
        {
            if (_items.Values.Any(t => t.Id != excludedId && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DuplicateTemplateNameException(name);
            }
        }

        // Prolazi kroz JSON kao prava baza, da test uhvati sve što serijalizacija gubi.
        private static SavedTemplate ToSaved(int id, TemplateDraft draft, string fingerprint)
        {
            var (mapping, rules, constants) = TemplateContentSerializer.Serialize(draft.Content);
            return new SavedTemplate(
                id, draft.Name, draft.ConnectionId, draft.SchemaName, draft.TableName, draft.SheetName, draft.HeaderRow, fingerprint,
                TemplateContentSerializer.Deserialize(mapping, rules, constants), DateTime.UtcNow, DateTime.UtcNow);
        }
    }
}