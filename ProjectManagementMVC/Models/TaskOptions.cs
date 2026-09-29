using System.ComponentModel.DataAnnotations;

namespace ProjectManagementMVC.Models;

// Enum tekee numeroiden merkityksen näkyväksi koodissa ja pudotusvalikoissa.
// EF tallentaa arvot edelleen int-sarakkeisiin, joten tauluja ei tarvitse luoda uudelleen.
public enum WorkStatus
{
    [Display(Name = "Avoin")] Open = 1,
    [Display(Name = "Työn alla")] InProgress = 2,
    [Display(Name = "Valmis")] Done = 3
}

public enum TaskPriority
{
    [Display(Name = "Matala")] Low = 1,
    [Display(Name = "Normaali")] Normal = 2,
    [Display(Name = "Korkea")] High = 3
}
