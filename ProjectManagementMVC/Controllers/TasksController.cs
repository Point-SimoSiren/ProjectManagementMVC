using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectManagementMVC.Models;
using ProjectManagementMVC.ViewModels;
using ProjectTask = ProjectManagementMVC.Models.Task;

namespace ProjectManagementMVC.Controllers;

public class TasksController(TaskDbContext context, ILogger<TasksController> logger) : CrudController(context, logger)
{
    public async Task<IActionResult> Index(string? search, int? userId, int? projectId, WorkStatus? status)
    {
        var query = Db.Tasks.AsNoTracking().Include(t => t.User).Include(t => t.Project).AsQueryable();
        search = search?.Trim();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(t => t.Title.Contains(search) || (t.Description != null && t.Description.Contains(search))
                || (t.User.FirstName + " " + t.User.LastName).Contains(search) || t.User.Email.Contains(search)
                || (t.Project != null && t.Project.Name.Contains(search)));
        if (userId.HasValue) query = query.Where(t => t.UserId == userId);
        if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId);
        if (status.HasValue) query = query.Where(t => t.Status == status);
        return View(new ListViewModel<ProjectTask> { Search = search, UserId = userId, ProjectId = projectId,
            Status = status, Items = await query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.TaskId).ToListAsync(),
            Users = await UserOptions(), Projects = await ProjectOptions() });
    }

    public async Task<IActionResult> Details(int id)
    {
        var task = await ReadTask(id);
        return task is null ? NotFound() : View(task);
    }

    public async Task<IActionResult> Create()
    {
        var form = new TaskForm();
        await PopulateOptions(form);
        return View(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaskForm form)
    {
        await ValidateRelations(form);
        if (ModelState.IsValid)
        {
            var now = DateTime.Now;
            Db.Tasks.Add(new ProjectTask { Title = form.Title.Trim(), Description = form.Description?.Trim(),
                UserId = form.UserId!.Value, ProjectId = form.ProjectId, Status = form.Status, Priority = form.Priority,
                CreatedAt = now, StatusChanged = now });
            if (await TrySave()) return RedirectToAction(nameof(Index));
        }
        await PopulateOptions(form);
        return View(form);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var task = await Db.Tasks.FindAsync(id);
        if (task is null) return NotFound();
        var form = new TaskForm { Title = task.Title, Description = task.Description, UserId = task.UserId,
            ProjectId = task.ProjectId, Status = task.Status, Priority = task.Priority };
        await PopulateOptions(form);
        return View(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TaskForm form)
    {
        var task = await Db.Tasks.FindAsync(id);
        if (task is null) return NotFound();
        await ValidateRelations(form);
        if (ModelState.IsValid)
        {
            // Aikaleima muuttuu vain tilan vaihtuessa; otsikon korjaus ei ole tilan muutos.
            if (task.Status != form.Status) task.StatusChanged = DateTime.Now;
            task.Title = form.Title.Trim();
            task.Description = form.Description?.Trim();
            task.UserId = form.UserId!.Value;
            task.ProjectId = form.ProjectId;
            task.Status = form.Status;
            task.Priority = form.Priority;
            if (await TrySave()) return RedirectToAction(nameof(Index));
        }
        await PopulateOptions(form);
        return View(form);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var task = await ReadTask(id);
        return task is null ? NotFound() : View(task);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var task = await Db.Tasks.Include(t => t.User).Include(t => t.Project).FirstOrDefaultAsync(t => t.TaskId == id);
        if (task is null) return NotFound();
        Db.Tasks.Remove(task);
        if (await TrySave()) return RedirectToAction(nameof(Index));
        return View("Delete", task);
    }

    private Task<ProjectTask?> ReadTask(int id) => Db.Tasks.AsNoTracking()
        .Include(t => t.User).Include(t => t.Project).FirstOrDefaultAsync(t => t.TaskId == id);

    private async System.Threading.Tasks.Task ValidateRelations(TaskForm form)
    {
        await ValidateUser(form.UserId);
        if (form.ProjectId.HasValue && !await Db.Projects.AnyAsync(p => p.ProjectId == form.ProjectId))
            ModelState.AddModelError(nameof(form.ProjectId), "Valittua projektia ei enää ole. Valitse toinen projekti.");
    }

    private async System.Threading.Tasks.Task PopulateOptions(TaskForm form)
    {
        form.Users = await UserOptions();
        form.Projects = await ProjectOptions();
    }
}
