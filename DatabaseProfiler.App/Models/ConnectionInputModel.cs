namespace DatabaseProfiler.App.Models;

public enum AuthenticationMethod
{
    WindowsTrustedPassThrough,
    SqlServer
}

public sealed class ConnectionInputModel
{
    public string? ServerName { get; set; }

    public AuthenticationMethod AuthenticationMethod { get; set; } = AuthenticationMethod.WindowsTrustedPassThrough;

    public string? UserName { get; set; }

    public string? Password { get; set; }
}
