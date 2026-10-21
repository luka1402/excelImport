using Microsoft.Extensions.Options;

namespace ExcelImporter.Api;

/// <summary>Neispravna konfiguracija se prijavljuje jednom jasnom porukom umesto stack trace-a iz <c>app.Run()</c>.</summary>
public static class StartupErrorReporter
{
    public static int Run(Action run, TextWriter error)
    {
        try
        {
            run();
            return 0;
        }
        catch (OptionsValidationException exception)
        {
            error.WriteLine("Aplikacija ne moze da se pokrene zbog neispravne konfiguracije:");
            foreach (var failure in exception.Failures)
            {
                error.WriteLine($" - {failure}");
            }

            return 1;
        }
    }
}
