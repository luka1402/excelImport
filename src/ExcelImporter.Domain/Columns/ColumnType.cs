namespace ExcelImporter.Domain.Columns;

public enum ColumnType
{
    Text,
    WholeNumber,
    DecimalNumber,
    Date,
    Boolean,
}

/// <summary>
/// Opis jedne kolone. Kolona bez ijedne vrednosti je <see cref="ColumnType.Text"/> sa <see cref="IsEmpty"/>;
/// kolona sa vrednostima različitih vrsta (npr. brojevi i tekst) je <see cref="ColumnType.Text"/> sa <see cref="IsMixed"/>.
/// </summary>
public sealed record ColumnProfile(int Index, string Name, ColumnType Type, bool IsEmpty, bool IsMixed, int NonEmptyCount);

/// <summary>
/// Struktura sheet-a. <see cref="HeaderRowIndex"/> (0-based) je <c>null</c> kada header nije pronađen;
/// tada su svi redovi podaci, a kolone dobijaju imena "Kolona N".
/// </summary>
public sealed record SheetStructure(int? HeaderRowIndex, bool HeaderChosenManually, IReadOnlyList<ColumnProfile> Columns);
