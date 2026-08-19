namespace MAIPT.PM.Api.Data;

public static class AuditActor
{
    private static readonly AsyncLocal<long?> Current = new();
    public static long? UserId
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
