using ExcelImporter.Application.Schema;

namespace ExcelImporter.Application.Templates;

/// <summary>Trajno čuvanje šablona. Ime je jedinstveno (bez obzira na velika/mala slova).</summary>
public interface ITemplateStore
{
    Task<IReadOnlyList<TemplateSummary>> ListAsync(CancellationToken cancellationToken);

    Task<SavedTemplate?> FindAsync(int id, CancellationToken cancellationToken);

    /// <exception cref="DuplicateTemplateNameException">Ime već koristi drugi šablon.</exception>
    Task<SavedTemplate> AddAsync(TemplateDraft draft, string columnsFingerprint, CancellationToken cancellationToken);

    /// <exception cref="DuplicateTemplateNameException">Ime već koristi drugi šablon.</exception>
    /// <returns><c>null</c> ako šablon ne postoji.</returns>
    Task<SavedTemplate?> UpdateAsync(int id, TemplateDraft draft, string columnsFingerprint, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}

/// <summary>Vraća šemu ciljne baze za konekciju iz šablona (connection string se razrešava u infrastrukturi).</summary>
public interface ITemplateSchemaSource
{
    Task<DatabaseSchema> GetSchemaAsync(int connectionId, CancellationToken cancellationToken);
}

public sealed class TemplateService(ITemplateStore store, ITemplateSchemaSource schemaSource)
{
    public Task<IReadOnlyList<TemplateSummary>> ListAsync(CancellationToken cancellationToken) =>
        store.ListAsync(cancellationToken);

    public async Task<SavedTemplate> GetAsync(int id, CancellationToken cancellationToken) =>
        await store.FindAsync(id, cancellationToken) ?? throw new TemplateNotFoundException(id);

    /// <exception cref="TemplateInvalidException">Šablon referencira tabelu/kolonu koje nema u šemi ili je neispravan.</exception>
    /// <exception cref="DuplicateTemplateNameException"/>
    public async Task<SavedTemplate> CreateAsync(TemplateDraft draft, CancellationToken cancellationToken)
    {
        var normalized = await PrepareAsync(draft, cancellationToken);
        return await store.AddAsync(normalized.Draft, normalized.Fingerprint, cancellationToken);
    }

    public async Task<SavedTemplate> UpdateAsync(int id, TemplateDraft draft, CancellationToken cancellationToken)
    {
        var normalized = await PrepareAsync(draft, cancellationToken);
        return await store.UpdateAsync(id, normalized.Draft, normalized.Fingerprint, cancellationToken)
            ?? throw new TemplateNotFoundException(id);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        if (!await store.DeleteAsync(id, cancellationToken))
        {
            throw new TemplateNotFoundException(id);
        }
    }

    /// <summary>Primena šablona na kolone novog fajla; šema se čita sveža iz keša, pa izmena ciljne baze označava šablon neupotrebljivim.</summary>
    public async Task<TemplateApplication> ApplyAsync(int id, IReadOnlyList<string> fileColumns, CancellationToken cancellationToken)
    {
        var template = await GetAsync(id, cancellationToken);
        var schema = await schemaSource.GetSchemaAsync(template.ConnectionId, cancellationToken);
        return TemplateApplicator.Apply(template, fileColumns, schema);
    }

    private async Task<(TemplateDraft Draft, string Fingerprint)> PrepareAsync(TemplateDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var schema = await schemaSource.GetSchemaAsync(draft.ConnectionId, cancellationToken);
        var errors = TemplateValidator.ValidateDraft(draft, schema);
        if (errors.Count > 0)
        {
            throw new TemplateInvalidException(errors);
        }

        // Kanonska imena iz šeme, da se u bazi ne čuva ono što je korisnik otkucao u drugom slovu.
        var table = schema.FindTable(draft.SchemaName, draft.TableName)!;
        var canonical = draft with
        {
            Name = draft.Name.Trim(),
            SchemaName = table.Schema,
            TableName = table.Name,
        };
        return (canonical, ColumnsFingerprint.Compute(draft.Content.SourceColumns));
    }
}