using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProjectManagementMVC.Models;

namespace ProjectManagementMVC.Controllers;

// Yhteiset apumetodit eivät ole HTTP-toimintoja (protected). Varsinaiset CRUD-toiminnot
// ovat omissa controllereissaan, jotta pyyntöjen kulkua on helppo seurata.
public abstract class CrudController(TaskDbContext context, ILogger logger) : Controller
{
    protected readonly TaskDbContext Db = context;

    protected Task<List<SelectListItem>> UserOptions() => Db.Users.AsNoTracking()
        .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
        .Select(u => new SelectListItem(u.FirstName + " " + u.LastName + " (" + u.Email + ")", u.UserId.ToString()))
        .ToListAsync();

    protected Task<List<SelectListItem>> ProjectOptions() => Db.Projects.AsNoTracking()
        .OrderBy(p => p.Name)
        .Select(p => new SelectListItem(p.Name, p.ProjectId.ToString())).ToListAsync();

    protected async System.Threading.Tasks.Task ValidateUser(int? userId)
    {
        // Valikko ei yksin takaa kelvollisuutta: myös käsin lähetetty tunniste tarkistetaan.
        if (userId.HasValue && !await Db.Users.AnyAsync(u => u.UserId == userId))
            ModelState.AddModelError("UserId", "Valittua käyttäjää ei enää ole. Valitse toinen käyttäjä.");
    }

    protected async Task<bool> TrySave()
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
