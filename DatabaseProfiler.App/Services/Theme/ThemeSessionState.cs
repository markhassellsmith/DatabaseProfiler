using Microsoft.AspNetCore.Http;

namespace DatabaseProfiler.App.Services.Theme;

public enum AppTheme
{
    Light,
    Dark,
    Ocean
}

public static class ThemeSessionState
{
    private const string SessionKey = "DatabaseProfiler.AppTheme";
    private const string CookieKey = "DatabaseProfiler.AppTheme";

    public static AppTheme GetTheme(this ISession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var value = session.GetString(SessionKey);
        return Enum.TryParse(value, ignoreCase: true, out AppTheme theme)
            ? theme
            : AppTheme.Light;
    }

    public static void SetTheme(this ISession session, AppTheme theme)
    {
        ArgumentNullException.ThrowIfNull(session);

        session.SetString(SessionKey, theme.ToString());
    }

    public static AppTheme GetTheme(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Request.Cookies.TryGetValue(CookieKey, out var value)
            && Enum.TryParse(value, ignoreCase: true, out AppTheme theme))
        {
            return theme;
        }

        return httpContext.Session.GetTheme();
    }

    public static void SetTheme(this HttpContext httpContext, AppTheme theme)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Session.SetTheme(theme);
        httpContext.Response.Cookies.Append(CookieKey, theme.ToString(), new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps
        });
    }
}
