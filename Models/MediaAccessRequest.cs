using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

public enum RequestStatus { Draft, Submitted, ManagerApproved, SecurityApproved, Rejected, Completed,
    OnHold
}

public class MediaAccessRequest
{
    public int Id { get; set; }
public string? RequestNumber { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Draft;
    [NotMapped]
 public bool ConfirmDeclaration { get; set; } 

    public string EmploymentStatus { get; set; } = ""; // Employee/Contractor
    public string Name { get; set; } = "";
    public string? EmployeeNumberOrEmployer { get; set; }
    public string? Title { get; set; }
    public string? OfficeExtension { get; set; }
    public string? Department { get; set; }
    public string? Directorate { get; set; }
    public string LoginName { get; set; } = ""; // samAccountName
    public string? UserMachineId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Classification { get; set; } = "";
    public string Justification { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBySam { get; set; } = "";
    public DateTime? RequesterSignAt { get; set; }
    public DateTime? ManagerSignAt { get; set; }
    public DateTime? SecuritySignAt { get; set; }
    public DateTime? ITSignAt { get; set; }
  // === NEW: email approval tokens ===
    public string? EmailActionToken { get; set; }
    public DateTime? TokenExpiresAt { get; set; } 
       public List<RequestDecision> Decisions { get; set; } = new();
}
