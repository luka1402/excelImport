using ExcelImporter.Application.Templates;
using ExcelImporter.Domain.Entities;
using ExcelImporter.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ExcelImporter.Infrastructure.Templates;

public sealed class EfTemplateStore(ExcelImporterDbContext context, TimeProvider clock) : ITemplateStore
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task<IReadOnlyList<TemplateSummary>> ListAsync(CancellationToken cancellationToken) =>
        await context.Templates
            .AsNoTracking()
            .OrderBy(template => template.Name)
            .Select(template => new TemplateSummary(
                template.Id, template.Name, template.ConnectionId, template.SchemaName, template.TableName, template.UpdatedAt))
            .ToListAsync(cancellationToken);

    public async Task<SavedTemplate?> FindAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await context.Templates.AsNoTracking().SingleOrDefaultAsync(template => template.Id == id, cancellationToken);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<SavedTemplate> AddAsync(TemplateDraft draft, string columnsFingerprint, CancellationToken cancellationToken)
    {
        await EnsureNameIsFreeAsync(draft.Name, excludedId: null, cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        var (mappingJson, rulesJson, constantsJson) = TemplateContentSerializer.Serialize(draft.Content);
        var entity = new ImportTemplate
        {
            Name = draft.Name,
            ColumnsFingerprint = columnsFingerprint,
            MappingJson = mappingJson,
            RulesJson = rulesJson,
            ConstantsJson = constantsJson,
            SchemaName = draft.SchemaName,
            TableName = draft.TableName,
            CreatedAt = now,
            UpdatedAt = now,
        };
        Assign(entity, draft);

        context.Templates.Add(entity);
        await SaveAsync(draft.Name, cancellationToken);
        return ToModel(entity);
    }

    public async Task<SavedTemplate?> UpdateAsync(int id, TemplateDraft draft, string columnsFingerprint, CancellationToken cancellationToken)
    {
        var entity = await context.Templates.SingleOrDefaultAsync(template => template.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        await EnsureNameIsFreeAsync(draft.Name, id, cancellationToken);

        (entity.MappingJson, entity.RulesJson, entity.ConstantsJson) = TemplateContentSerializer.Serialize(draft.Content);
        entity.ColumnsFingerprint = columnsFingerprint;
        entity.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        Assign(entity, draft);

        await SaveAsync(draft.Name, cancellationToken);
        return ToModel(entity);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        // Istorija uvoza ima FK ka šablonu, pa se brisanje bez ORM učitavanja oslanja na bazu da odbije ako je u upotrebi.
        var deleted = await context.Templates.Where(template => template.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    private static void Assign(ImportTemplate entity, TemplateDraft draft)
    {
        entity.Name = draft.Name;
        entity.ConnectionId = draft.ConnectionId;
        entity.SchemaName = draft.SchemaName;
        entity.TableName = draft.TableName;
        entity.SheetName = draft.SheetName;
        entity.HeaderRow = draft.HeaderRow;
    }

    // Provera unapred daje jasnu grešku; jedinstveni indeks i dalje štiti od trke dva istovremena čuvanja.
    private async Task EnsureNameIsFreeAsync(string name, int? excludedId, CancellationToken cancellationToken)
    {
        var taken = await context.Templates.AnyAsync(template => template.Name == name && template.Id != excludedId, cancellationToken);
        if (taken)
        {
            throw new DuplicateTemplateNameException(name);
        }
    }

    private async Task SaveAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            throw new DuplicateTemplateNameException(name);
        }
    }

    private static SavedTemplate ToModel(ImportTemplate entity) => new(
        entity.Id,
        entity.Name,
        entity.ConnectionId,
        entity.SchemaName,
        entity.TableName,
        entity.SheetName,
        entity.HeaderRow,
        entity.ColumnsFingerprint,
        TemplateContentSerializer.Deserialize(entity.MappingJson, entity.RulesJson, entity.ConstantsJson),
        entity.CreatedAt,
        entity.UpdatedAt);
}