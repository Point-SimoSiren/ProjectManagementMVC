using System.ComponentModel.DataAnnotations;

namespace ProjectManagementMVC.ViewModels;

// Lomakemalli sisältää vain käyttäjän muokattavat kentät. EF:n navigaatioita
// (Projects, Tasks) tai palvelimen aikaleimoja ei oteta vastaan selaimesta.
public class UserForm
{
    [Required(ErrorMessage = "Anna etunimi."), StringLength(50), Display(Name = "Etunimi")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Anna sukunimi."), StringLength(50), Display(Name = "Sukunimi")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Anna sähköposti."), StringLength(100), EmailAddress(ErrorMessage = "Anna kelvollinen sähköpostiosoite."), Display(Name = "Sähköposti")]
    public string Email { get; set; } = "";
}
