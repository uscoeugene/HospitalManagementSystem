using System;
using System.ComponentModel.DataAnnotations;

namespace HMS.UI.Models.Profile;

public class UserProfileViewModel
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    [Display(Name = "First name")]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Last name")]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Other names")]
    [StringLength(200)]
    public string OtherNames { get; set; } = string.Empty;

    [StringLength(32)]
    public string Gender { get; set; } = string.Empty;

    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    [Display(Name = "Phone number")]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;

    [Display(Name = "Staff number")]
    [StringLength(50)]
    public string StaffNumber { get; set; } = string.Empty;

    [StringLength(150)]
    public string Department { get; set; } = string.Empty;

    [Display(Name = "Job title")]
    [StringLength(150)]
    public string JobTitle { get; set; } = string.Empty;

    [Display(Name = "Medical staff")]
    public bool IsMedicalStaff { get; set; }
}
