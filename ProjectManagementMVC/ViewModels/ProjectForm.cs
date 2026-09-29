using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ProjectManagementMVC.ViewModels;

public class ProjectForm
{
    [Required(ErrorMessage = "Anna projektin nimi."), StringLength(100), Display(Name = "Nimi")]
    public string Name { get; set; } = "";

    [StringLength(1000), Display(Name = "Kuvaus")]
    public string? Description { get; set; }

    // Lomake lähettää vain vierasavaimen. Kokonaista User-oliota ei syötetä.
    [Required(ErrorMessage = "Valitse omistaja."), Range(1, int.MaxValue), Display(Name = "Omistaja")]
    public int? UserId { get; set; }

    // Vaihtoehdot haetaan palvelimella uudelleen myös virheellisen POST-pyynnön jälkeen.
    [ValidateNever]
    public List<SelectListItem> Users { get; set; } = [];
}
