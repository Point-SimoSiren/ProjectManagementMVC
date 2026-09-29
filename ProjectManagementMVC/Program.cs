using Microsoft.EntityFrameworkCore;
using ProjectManagementMVC.Models;
var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("TaskDbContext") ?? throw new InvalidOperationException("Connection string 'TaskDbContext' not found.");

// Riippuvuuksien injektointi (DI) antaa controllerille contextin yhden HTTP-pyynnön ajaksi.
// Kaikki controllerit käyttävät näin samaa asetustiedoston yhteysmääritystä.
builder.Services.AddDbContext<TaskDbContext>(options => options.UseSqlServer(connectionString));

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

// /Projects/Edit/5 sitoo arvon 5 parametrille id. Actionin parametrin nimen on vastattava reittiä.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Users}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

// Julkinen osittainen luokka mahdollistaa HTTP-integraatiotestit WebApplicationFactorylla.
public partial class Program { }
