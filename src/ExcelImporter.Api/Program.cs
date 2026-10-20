var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();

// Potrebno da WebApplicationFactory<Program> može da se koristi u Api.Tests.
public partial class Program;
