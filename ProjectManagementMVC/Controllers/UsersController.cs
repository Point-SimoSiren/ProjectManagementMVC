using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectManagementMVC.Models;
using ProjectManagementMVC.ViewModels;

namespace ProjectManagementMVC.Controllers;

public class UsersController(TaskDbContext context, ILogger<UsersController> logger) : Controller
{
    // DI antaa tietokantayhteyden tälle controllerille yhden HTTP-pyynnön ajaksi.
    private readonly TaskDbContext Db = context;

    public async Task<IActionResult> Index(string? search)
    {
        // IQueryable rakentaa SQL-kyselyn. Suodatus suoritetaan tietokannassa ennen ToListAsync-kutsua.
        var query = Db.Users.AsNoTracking();
        search = search?.Trim();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => (u.FirstName + " " + u.LastName).Contains(search) || u.Email.Contains(search));
        return View(new ListViewModel<User> { Search = search,
            Items = await query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToListAsync() });
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await ReadUser(id);
        return user is null ? NotFound() : View(user);
    }

    public IActionResult Create() => View(new UserForm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserForm form)
    {
        await ValidateEmail(form);
        if (ModelState.IsValid)
        {
            // Tunnisteen tuottaa tietokanta ja aikaleiman palvelin, ei selaimen lomake.
            Db.Users.Add(new User { FirstName = form.FirstName.Trim(), LastName = form.LastName.Trim(),
                Email = form.Email.Trim(), CreatedAt = DateTime.Now });
            if (await TrySave()) return RedirectToAction(nameof(Index));
        }
        return View(form);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var user = await Db.Users.FindAsync(id);
        return user is null ? NotFound() : View(new UserForm
            { FirstName = user.FirstName, LastName = user.LastName, Email = user.Email });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UserForm form)
    {
        var user = await Db.Users.FindAsync(id);
        if (user is null) return NotFound();
        await ValidateEmail(form, id);
        if (ModelState.IsValid)
        {
            // Muutetaan haettua oliota: CreatedAt ja relaatiot säilyvät ennallaan.
            user.FirstName = form.FirstName.Trim();
            user.LastName = form.LastName.Trim();
            user.Email = form.Email.Trim();
            if (await TrySave()) return RedirectToAction(nameof(Index));
        }
        return View(form);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var user = await ReadUser(id);
        return user is null ? NotFound() : View(user);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var user = await Db.Users.Include(u => u.Projects).Include(u => u.Tasks).AsSplitQuery().FirstOrDefaultAsync(u => u.UserId == id);
        if (user is null) return NotFound();
        // Pakollista vierasavainta ei voi tyhjentää. Siirrä liittyvät tiedot ensin toiselle henkilölle.
        if (user.Projects.Count > 0 || user.Tasks.Count > 0)
            ModelState.AddModelError("", "Siirrä käyttäjän projektit ja tehtävät toiselle käyttäjälle tai poista ne ensin.");
        else
        {
            Db.Users.Remove(user);
            if (await TrySave()) return RedirectToAction(nameof(Index));
        }
        return View("Delete", user);
    }

    // Kaksi kokoelmaa luetaan erillisillä kyselyillä, jotta projektien ja tehtävien rivit eivät monistu keskenään.
    private Task<User?> ReadUser(int id) => Db.Users.AsNoTracking().AsSplitQuery()
        .Include(u => u.Projects).Include(u => u.Tasks).FirstOrDefaultAsync(u => u.UserId == id);

    private async System.Threading.Tasks.Task ValidateEmail(UserForm form, int? id = null)
    {
        if (!string.IsNullOrWhiteSpace(form.Email) &&
            await Db.Users.AnyAsync(u => u.Email == form.Email.Trim() && u.UserId != id))
            ModelState.AddModelError(nameof(form.Email), "Sähköpostiosoite on jo käytössä.");
    }

    // Yksityiset apumetodit kuuluvat vain tähän controlleriin, eivätkä ole HTTP-toimintoja.
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
