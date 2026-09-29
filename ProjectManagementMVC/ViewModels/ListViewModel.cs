using Microsoft.AspNetCore.Mvc.Rendering;
using ProjectManagementMVC.Models;

namespace ProjectManagementMVC.ViewModels;

// Sama listan rakenne sopii kolmelle taululle. GET-lomakkeen hakuehdot säilyvät URL:ssa.
public class ListViewModel<T>
{
    public List<T> Items { get; set; } = [];
    public string? Search { get; set; }
    public int? UserId { get; set; }
    public int? ProjectId { get; set; }
    public WorkStatus? Status { get; set; }
    public List<SelectListItem> Users { get; set; } = [];
    public List<SelectListItem> Projects { get; set; } = [];
}
