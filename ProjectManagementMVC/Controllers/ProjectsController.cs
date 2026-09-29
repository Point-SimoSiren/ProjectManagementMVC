using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectManagementMVC.Models;
using ProjectManagementMVC.ViewModels;

namespace ProjectManagementMVC.Controllers;

public class ProjectsController(TaskDbContext context, ILogger<ProjectsController> logger) : CrudController(context, logger)
{
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
}
