using System;
using System.Collections.Generic;

namespace ProjectManagementMVC.Models;

public partial class Project
{
    public int ProjectId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    // Vierasavain viittaa olemassa olevaan omistajaan; nimi ja sähköposti ovat Users-taulussa.
    public int UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();

    public virtual User User { get; set; } = null!;
}
