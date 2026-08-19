namespace MAIPT.PM.Api.Models;

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public class ResetPasswordRequest
{
    public string? TemporaryPassword { get; set; }
}

public class PerformancePeriodCreateRequest
{
    public string Period { get; set; } = "";
    public string Level { get; set; } = "INDIVIDUAL";
    public long? UserId { get; set; }
    public string EmployeeName { get; set; } = "";
    public string Department { get; set; } = "IT";
    public string SourceSheet { get; set; } = "MPMS";
    public string Status { get; set; } = "OPEN";
}
