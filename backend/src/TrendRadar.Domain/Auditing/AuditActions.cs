namespace TrendRadar.Domain.Auditing;

public static class AuditActions
{
    public const string Create = "CREATE";
    public const string StatusChange = "STATUS_CHANGE";
    public const string Version = "VERSION";
    public const string Reclassify = "RECLASSIFY";
    public const string AddEvidence = "ADD_EVIDENCE";
    public const string Import = "IMPORT";
    public const string Correct = "CORRECT";
    public const string Resolve = "RESOLVE";
    public const string Dispute = "DISPUTE";
    public const string Withdraw = "WITHDRAW";
    public const string Login = "LOGIN";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string Logout = "LOGOUT";
    public const string RefreshTokenReuse = "REFRESH_TOKEN_REUSE";
}
