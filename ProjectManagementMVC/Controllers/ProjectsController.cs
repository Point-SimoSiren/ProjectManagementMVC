using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProjectManagementMVC.Models;
using ProjectManagementMVC.ViewModels;

namespace ProjectManagementMVC.Controllers;

public class ProjectsController(TaskDbContext context, ILogger<ProjectsController> logger) : Controller
{
    // DI antaa tietokantayhteyden tälle controllerille yhden HTTP-pyynnön ajaksi.
    private readonly TaskDbContext Db = context;

    public async Task<IActionResult> Index(string? search, int? userId)
    {
        // Include lataa omistajan näkymään: näytetään nimi pelkän avaimen sijaan.
        var query = Db.Projects.AsNoTracking().Include(p => p.User).AsQueryable();
        search = search?.Trim();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Name.Contains(search) || (p.Description != null && p.Description.Contains(search))
                || (p.User.FirstName + " " + p.User.LastName).Contains(search) || p.User.Email.Contains(search));
        if (userId.HasValue) query = query.Where(p => p.UserId == userId);
        return View(new ListViewModel<Project> { Search = search, UserId = userId,
            Items = await query.OrderBy(p => p.Name).ToListAsync(), Users = await UserOptions() });
    }

    public async Task<IActionResult> Details(int id)
    {
        var project = await ReadProject(id);
        return project is null ? NotFound() : View(project);
    }

    public async Task<IActionResult> Create() => View(new ProjectForm { Users = await UserOptions() });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProjectForm form)
    {
        await ValidateUser(form.UserId);
        if (ModelState.IsValid)
        {
            Db.Projects.Add(new Project { Name = form.Name.Trim(), Description = form.Description?.Trim(),
                UserId = form.UserId!.Value, CreatedAt = DateTime.Now });
            if (await TrySave()) return RedirectToAction(nameof(Index));
        }
        form.Users = await UserOptions();
        return View(form);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var project = await Db.Projects.FindAsync(id);
        return project is null ? NotFound() : View(new ProjectForm { Name = project.Name,
            Description = project.Description, UserId = project.UserId, Users = await UserOptions() });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProjectForm form)
    {
        var project = await Db.Projects.FindAsync(id);
        if (project is null) return NotFound();
        await ValidateUser(form.UserId);
        if (ModelState.IsValid)
        {
            project.Name = form.Name.Trim();
            project.Description = form.Description?.Trim();
            project.UserId = form.UserId!.Value;
            if (await TrySave()) return RedirectToAction(nameof(Index));
        }
        form.Users = await UserOptions();
        return View(form);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var project = await ReadProject(id);
        return project is null ? NotFound() : View(project);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var project = await Db.Projects.Include(p => p.User).Include(p => p.Tasks).FirstOrDefaultAsync(p => p.ProjectId == id);
        if (project is null) return NotFound();
        // Poisto ei hävitä tehtäviä huomaamatta. Tehtävästä voi myös poistaa projektivalinnan.
        if (project.Tasks.Count > 0)
            ModelState.AddModelError("", "Siirrä projektin tehtävät, valitse niille Ei projektia tai poista ne ensin.");
        else
        {
            Db.Projects.Remove(project);
            if (await TrySave()) return RedirectToAction(nameof(Index));
        }
        return View("Delete", project);
    }

    private Task<Project?> ReadProject(int id) => Db.Projects.AsNoTracking()
        .Include(p => p.User).Include(p => p.Tasks).FirstOrDefaultAsync(p => p.ProjectId == id);

    // Yksityiset apumetodit kuuluvat vain tähän controlleriin, eivätkä ole HTTP-toimintoja.
    private Task<List<SelectListItem>> UserOptions() => Db.Users.AsNoTracking()
        .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
        .Select(u => new SelectListItem(u.FirstName + " " + u.LastName + " (" + u.Email + ")", u.UserId.ToString()))
        .ToListAsync();

    private async System.Threading.Tasks.Task ValidateUser(int? userId)
    {
        // Valikko ei yksin takaa kelvollisuutta: myös käsin lähetetty tunniste tarkistetaan.
        if (userId.HasValue && !await Db.Users.AnyAsync(u => u.UserId == userId))
            ModelState.AddModelError("UserId", "Valittua käyttäjää ei enää ole. Valitse toinen käyttäjä.");
    }

    private async Task<bool> TrySave()
    {
        try
        {
            await Db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Tietue muuttui tallennuksen aikana.");
            ModelState.AddModelError("", "Tietue on poistettu tai muuttunut. Päivitä sivu ja yritä uudelleen.");
        }
        catch (DbUpdateException ex)
        {
            // Tietokannan rajoitteet ovat viimeinen suoja myös samanaikaisissa pyynnöissä.
            logger.LogWarning(ex, "Tietokanta hylkäsi muutoksen.");
            ModelState.AddModelError("", "Tallennus epäonnistui. Sähköposti voi olla jo käytössä tai liittyvä tietue on muuttunut. Poisto edellyttää, ettei tietueeseen ole viittauksia.");
        }
        return false;
    }
}
