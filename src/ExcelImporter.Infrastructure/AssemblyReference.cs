using System.Reflection;

namespace ExcelImporter.Infrastructure;

/// <summary>Oznaka assembly-ja za arhitektonske testove i registraciju servisa.</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
