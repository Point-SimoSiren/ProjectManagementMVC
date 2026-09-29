using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProjectManagementMVC.Models;
using Xunit;
using AsyncTask = System.Threading.Tasks.Task;

namespace ProjectManagementMVC.Tests;

// Testit käyttävät oikeaa SQL Serveriä mutta aina uutta, yksilöllistä testitietokantaa.
// Testipalvelin ajaa reitityksen, validoinnin, Razor-näkymät ja CSRF-suojauksenkin.
public sealed class TestApplication : WebApplicationFactory<Program>
{
    private readonly string databaseName = "TaskDb_CrudTests_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION")
                ?? @"Server=DUUNIKONE\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True")
            { InitialCatalog = databaseName };
            services.RemoveAll<DbContextOptions<TaskDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<TaskDbContext>>();
            services.AddDbContext<TaskDbContext>(o => o.UseSqlServer(connection.ConnectionString));
        });
    }

    public T Read<T>(Func<TaskDbContext, T> query)
    {
        using var scope = Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<TaskDbContext>());
    }

    public override async ValueTask DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
        // Älä koskaan poista muuta kantaa, vaikka testien konfiguraatiota muutettaisiin.
        if (db.Database.GetDbConnection().Database != databaseName)
            throw new InvalidOperationException("Testitietokannan nimi ei täsmää.");
        await db.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }
}

public class CrudTests : IClassFixture<TestApplication>
{
    private readonly TestApplication app;
    private readonly HttpClient client;

    public CrudTests(TestApplication app)
    {
        this.app = app;
        app.Read(db => db.Database.EnsureCreated());
        client = app.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
    }

    private async Task<string> Get(string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> Post(string path, Dictionary<string, string> fields)
    {
        var html = await Get(path);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "POST-lomakkeessa pitää olla CSRF-token.");
        Assert.Contains($"action=\"{path}\"", html);
        fields["__RequestVerificationToken"] = token.Groups[1].Value;
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    private static Dictionary<string, string> UserFields(string email) => new()
        { ["FirstName"] = "Testi", ["LastName"] = "Opiskelija", ["Email"] = email };

    private async Task<int> CreateUser()
    {
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        Assert.Equal(HttpStatusCode.Redirect, (await Post("/Users/Create", UserFields(email))).StatusCode);
        return app.Read(db => db.Users.Single(u => u.Email == email).UserId);
    }

    [Fact]
    public async AsyncTask All_tables_support_crud_search_and_relationship_guards()
    {
        var userId = await CreateUser();
        var originalUser = app.Read(db => db.Users.Single(u => u.UserId == userId));
        var userFields = UserFields(originalUser.Email);
        userFields["FirstName"] = "Muokattu";
        userFields["CreatedAt"] = "2000-01-01"; // Ylimääräistä selaimesta tullutta kenttää ei hyväksytä.
        Assert.Equal(HttpStatusCode.Redirect, (await Post($"/Users/Edit/{userId}", userFields)).StatusCode);
        Assert.Equal(originalUser.CreatedAt, app.Read(db => db.Users.Single(u => u.UserId == userId).CreatedAt));
        Assert.Contains("Muokattu", await Get($"/Users/Details/{userId}"));
        Assert.Contains(originalUser.Email, await Get("/Users?search=" + Uri.EscapeDataString(originalUser.Email)));
        Assert.DoesNotContain(originalUser.Email, await Get("/Users?search=not-found-" + Guid.NewGuid()));

        var name = "Projekti-" + Guid.NewGuid().ToString("N");
        var projectFields = new Dictionary<string, string> { ["Name"] = name, ["Description"] = "Hakusana",
            ["UserId"] = userId.ToString() };
        Assert.Equal(HttpStatusCode.Redirect, (await Post("/Projects/Create", projectFields)).StatusCode);
        var project = app.Read(db => db.Projects.Single(p => p.Name == name));
        projectFields["Name"] = name + "-uusi";
        Assert.Equal(HttpStatusCode.Redirect, (await Post($"/Projects/Edit/{project.ProjectId}", projectFields)).StatusCode);
        Assert.Equal(project.CreatedAt, app.Read(db => db.Projects.Single(p => p.ProjectId == project.ProjectId).CreatedAt));
        Assert.Contains(name + "-uusi", await Get($"/Projects/Details/{project.ProjectId}"));
        Assert.Contains(name, await Get($"/Projects?search=Hakusana&userId={userId}"));
        Assert.DoesNotContain(name, await Get("/Projects?search=impossible-" + Guid.NewGuid()));
        Assert.DoesNotContain(name, await Get("/Projects?userId=2147483647"));

        var title = "Tehtävä-" + Guid.NewGuid().ToString("N");
        var taskFields = new Dictionary<string, string> { ["Title"] = title, ["Description"] = "Kuvaushaku",
            ["UserId"] = userId.ToString(), ["ProjectId"] = project.ProjectId.ToString(), ["Status"] = "1", ["Priority"] = "2" };
        Assert.Equal(HttpStatusCode.Redirect, (await Post("/Tasks/Create", taskFields)).StatusCode);
        var task = app.Read(db => db.Tasks.Single(t => t.Title == title));
        Assert.NotNull(task.StatusChanged);
        Assert.Contains(title, await Get($"/Tasks/Details/{task.TaskId}"));
        Assert.Contains(title, await Get($"/Tasks?search=Kuvaushaku&userId={userId}&projectId={project.ProjectId}&status=1"));
        Assert.DoesNotContain(title, await Get($"/Tasks?userId={userId}&status=3"));
        Assert.DoesNotContain(title, await Get("/Tasks?projectId=2147483647"));
        Assert.DoesNotContain(title, await Get("/Tasks?search=impossible-" + Guid.NewGuid()));

        taskFields["Title"] = title + "!";
        taskFields["StatusChanged"] = "2000-01-01";
        Assert.Equal(HttpStatusCode.Redirect, (await Post($"/Tasks/Edit/{task.TaskId}", taskFields)).StatusCode);
        Assert.Equal(task.StatusChanged, app.Read(db => db.Tasks.Single(t => t.TaskId == task.TaskId).StatusChanged));
        taskFields["Status"] = "3";
        Assert.Equal(HttpStatusCode.Redirect, (await Post($"/Tasks/Edit/{task.TaskId}", taskFields)).StatusCode);
        var changed = app.Read(db => db.Tasks.Single(t => t.TaskId == task.TaskId));
        Assert.Equal(WorkStatus.Done, changed.Status);
        Assert.True(changed.StatusChanged > task.StatusChanged);
        Assert.Equal(task.CreatedAt, changed.CreatedAt);

        // Relaatioiden poistosuojat jättävät tietueet talteen ja palauttavat ymmärrettävän viestin.
        var userDelete = await Post($"/Users/Delete/{userId}", []);
        Assert.Equal(HttpStatusCode.OK, userDelete.StatusCode);
        Assert.Contains("Siirrä", WebUtility.HtmlDecode(await userDelete.Content.ReadAsStringAsync()));
        var projectDelete = await Post($"/Projects/Delete/{project.ProjectId}", []);
        Assert.Equal(HttpStatusCode.OK, projectDelete.StatusCode);
        Assert.True(app.Read(db => db.Projects.Any(p => p.ProjectId == project.ProjectId)));

        // Projektisuhde on valinnainen: sen irrotus sallii projektin poiston, tehtävä säilyy.
        taskFields["ProjectId"] = "";
        Assert.Equal(HttpStatusCode.Redirect, (await Post($"/Tasks/Edit/{task.TaskId}", taskFields)).StatusCode);
        Assert.Null(app.Read(db => db.Tasks.Single(t => t.TaskId == task.TaskId).ProjectId));
        Assert.Equal(HttpStatusCode.Redirect, (await Post($"/Projects/Delete/{project.ProjectId}", [])).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post($"/Tasks/Delete/{task.TaskId}", [])).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post($"/Users/Delete/{userId}", [])).StatusCode);
        Assert.False(app.Read(db => db.Tasks.Any(t => t.TaskId == task.TaskId)));
        Assert.False(app.Read(db => db.Projects.Any(p => p.ProjectId == project.ProjectId)));
        Assert.False(app.Read(db => db.Users.Any(u => u.UserId == userId)));
    }

    [Fact]
    public async AsyncTask Invalid_values_and_duplicate_email_are_reported_without_writing()
    {
        var userId = await CreateUser();
        var email = app.Read(db => db.Users.Single(u => u.UserId == userId).Email);
        var duplicate = await Post("/Users/Create", UserFields(email));
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("Sähköpostiosoite on jo käytössä.", WebUtility.HtmlDecode(await duplicate.Content.ReadAsStringAsync()));
        Assert.Equal(1, app.Read(db => db.Users.Count(u => u.Email == email)));
        var invalidEmail = await Post("/Users/Create", UserFields("ei-sahkoposti"));
        Assert.Equal(HttpStatusCode.OK, invalidEmail.StatusCode);
        Assert.Contains("kelvollinen sähköpostiosoite", WebUtility.HtmlDecode(await invalidEmail.Content.ReadAsStringAsync()));

        var name = "Ei tallennu " + Guid.NewGuid();
        var invalidProject = await Post("/Projects/Create", new() { ["Name"] = name, ["UserId"] = "2147483647" });
        Assert.Equal(HttpStatusCode.OK, invalidProject.StatusCode);
        var projectHtml = WebUtility.HtmlDecode(await invalidProject.Content.ReadAsStringAsync());
        Assert.Contains("Valittua käyttäjää ei enää ole", projectHtml);
        Assert.Contains(email, projectHtml); // Valikon vaihtoehdot säilyvät virhesivulla.
        Assert.False(app.Read(db => db.Projects.Any(p => p.Name == name)));

        var invalidTask = await Post("/Tasks/Create", new() { ["Title"] = "Virheellinen tehtävä",
            ["UserId"] = userId.ToString(), ["ProjectId"] = "2147483647", ["Status"] = "99", ["Priority"] = "99" });
        Assert.Equal(HttpStatusCode.OK, invalidTask.StatusCode);
        var html = WebUtility.HtmlDecode(await invalidTask.Content.ReadAsStringAsync());
        Assert.Contains("Valittua projektia ei enää ole", html);
        // Enum-mallinsidonta voi hylätä numeron jo ennen DataAnnotations-validointia.
        Assert.Matches("field-validation-error[^>]*data-valmsg-for=\"Status\"[^>]*>.+?</span>", html);
        Assert.Matches("field-validation-error[^>]*data-valmsg-for=\"Priority\"[^>]*>.+?</span>", html);
        Assert.Contains(email, html);
        Assert.False(app.Read(db => db.Tasks.Any(t => t.Title == "Virheellinen tehtävä")));
    }

    [Theory]
    [InlineData("Users")]
    [InlineData("Projects")]
    [InlineData("Tasks")]
    public async AsyncTask Required_fields_and_csrf_are_enforced(string controller)
    {
        var invalid = await Post($"/{controller}/Create", []);
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("field-validation-error", await invalid.Content.ReadAsStringAsync());
        var withoutToken = await client.PostAsync($"/{controller}/Create", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
    }

    [Theory]
    [InlineData("Users")]
    [InlineData("Projects")]
    [InlineData("Tasks")]
    public async AsyncTask Missing_records_return_404(string controller)
    {
        foreach (var action in new[] { "Details", "Edit", "Delete" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/{controller}/{action}/2147483647")).StatusCode);
    }

    [Fact]
    public async AsyncTask Task_can_be_created_without_project_and_user_can_change_email()
    {
        var userId = await CreateUser();
        var title = Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.Redirect, (await Post("/Tasks/Create", new() { ["Title"] = title,
            ["UserId"] = userId.ToString(), ["ProjectId"] = "", ["Status"] = "2", ["Priority"] = "3" })).StatusCode);
        var task = app.Read(db => db.Tasks.Single(t => t.Title == title));
        Assert.Null(task.ProjectId);
        Assert.Contains("Ei projektia", await Get($"/Tasks/Details/{task.TaskId}"));
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        Assert.Equal(HttpStatusCode.Redirect, (await Post($"/Users/Edit/{userId}", UserFields(email))).StatusCode);
        Assert.Equal(email, app.Read(db => db.Users.Single(u => u.UserId == userId).Email));
    }
}
