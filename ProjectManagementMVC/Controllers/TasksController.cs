using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProjectManagementMVC.Models;
// Task nimen konfliktin ratkaisu
using ProjectTask = ProjectManagementMVC.Models.Task;

public class TasksController : Controller
{
    private readonly TaskDbContext _context;

    public TasksController(TaskDbContext context)
    {
        _context = context;
    }

    // GET: Tasks
    public async Task<IActionResult> Index()
    {
        var tasks = _context.Tasks
            .Include(task => task.Project)
            .Include(task => task.User);

        return View(await tasks.ToListAsync());
    }

    // GET: Tasks/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var task = await _context.Tasks
            .Include(item => item.Project)
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.TaskId == id);

        if (task is null)
        {
            return NotFound();
        }

        return View(task);
    }

    // GET: Tasks/Create
    public IActionResult Create()
    {
        PopulateSelectLists();
        return View();
    }

    // POST: Tasks/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Title,Description,Status,StatusChanged,Priority,UserId,ProjectId")]
        ProjectTask task)
    {
        RemoveNavigationValidation();

        if (ModelState.IsValid)
        {
            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        PopulateSelectLists(task.UserId, task.ProjectId);
        return View(task);
    }

    // GET: Tasks/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var task = await _context.Tasks.FindAsync(id);
        if (task is null)
        {
            return NotFound();
        }

        PopulateSelectLists(task.UserId, task.ProjectId);
        return View(task);
    }

    // POST: Tasks/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("TaskId,Title,Description,Status,StatusChanged,Priority,UserId,ProjectId")]
        ProjectTask task)
    {
        if (id != task.TaskId)
        {
            return NotFound();
        }

        RemoveNavigationValidation();

        if (ModelState.IsValid)
        {
            try
            {
                var existingTask = await _context.Tasks.FindAsync(id);
                if (existingTask is null)
                {
                    return NotFound();
                }

                existingTask.Title = task.Title;
                existingTask.Description = task.Description;
                existingTask.Status = task.Status;
                existingTask.StatusChanged = task.StatusChanged;
                existingTask.Priority = task.Priority;
                existingTask.UserId = task.UserId;
                existingTask.ProjectId = task.ProjectId;

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await TaskExistsAsync(task.TaskId))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        PopulateSelectLists(task.UserId, task.ProjectId);
        return View(task);
    }

    // GET: Tasks/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var task = await _context.Tasks
            .Include(item => item.Project)
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.TaskId == id);

        if (task is null)
        {
            return NotFound();
        }

        return View(task);
    }

    // POST: Tasks/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var task = await _context.Tasks.FindAsync(id);
        if (task is not null)
        {
            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private void PopulateSelectLists(int? userId = null, int? projectId = null)
    {
        ViewData["UserId"] = new SelectList(_context.Users, "UserId", "Email", userId);
        ViewData["ProjectId"] = new SelectList(_context.Projects, "ProjectId", "Name", projectId);
    }

    private void RemoveNavigationValidation()
    {
        ModelState.Remove(nameof(ProjectTask.User));
        ModelState.Remove(nameof(ProjectTask.Project));
    }

    private Task<bool> TaskExistsAsync(int id)
    {
        return _context.Tasks.AnyAsync(task => task.TaskId == id);
    }
}
