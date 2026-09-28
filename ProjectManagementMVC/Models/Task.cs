using System;
using System.Collections.Generic;

namespace ProjectManagementMVC.Models;

public partial class Task
{
    public int TaskId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public int Status { get; set; }

    public DateTime? StatusChanged { get; set; }

    public int Priority { get; set; }

    public int UserId { get; set; }

    public int? ProjectId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Project? Project { get; set; }

    public virtual User User { get; set; } = null!;
}
