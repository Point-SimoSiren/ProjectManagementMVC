using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using ProjectManagementMVC.Models;

namespace ProjectManagementMVC.ViewModels;

public class TaskForm
{
    [Required(ErrorMessage = "Anna otsikko."), StringLength(50), Display(Name = "Otsikko")]
    public string Title { get; set; } = "";

    [StringLength(1000), Display(Name = "Kuvaus")]
    public string? Description { get; set; }

    [EnumDataType(typeof(WorkStatus), ErrorMessage = "Valitse kelvollinen tila."), Display(Name = "Tila")]
    public WorkStatus Status { get; set; } = WorkStatus.Open;

    [EnumDataType(typeof(TaskPriority), ErrorMessage = "Valitse kelvollinen prioriteetti."), Display(Name = "Prioriteetti")]
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    [Required(ErrorMessage = "Valitse vastuuhenkilö."), Range(1, int.MaxValue), Display(Name = "Vastuuhenkilö")]
    public int? UserId { get; set; }

    // Nullable-vierasavain tarkoittaa, että tehtävä saa olla ilman projektia.
    [Range(1, int.MaxValue), Display(Name = "Projekti")]
    public int? ProjectId { get; set; }

    [ValidateNever]
    public List<SelectListItem> Users { get; set; } = [];
    [ValidateNever]
    public List<SelectListItem> Projects { get; set; } = [];
}
