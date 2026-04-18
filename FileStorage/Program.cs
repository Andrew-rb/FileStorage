var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var storageRoot = Path.Combine(Directory.GetCurrentDirectory(), "StorageRoot");
if (!Directory.Exists(storageRoot))
    Directory.CreateDirectory(storageRoot);

builder.Services.AddSingleton(_ => storageRoot);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();