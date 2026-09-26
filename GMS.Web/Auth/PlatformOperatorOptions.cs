using GMS.Core.Security;

namespace GMS.Web.Auth;

/// <summary>
/// The system owner's credentials, read from configuration under "Platform:Operator".
/// </summary>
/// <remarks>
/// Deliberately not in the database. The operator console manages the tenants, so its
/// credentials must not live in the same table as theirs, and there is no organization
/// this account could belong to. Supply them through user-secrets or the environment:
///
///   Platform__Operator__UserName     = owner
///   Platform__Operator__PasswordHash = &lt;output of PlatformOperator.HashFor("...")&gt;
///
/// When either is missing the console is switched off entirely rather than falling back
/// to a default, so a deployment that has not configured it exposes no operator surface.
/// </remarks>
public sealed class PlatformOperatorOptions
{
    public const string Section = "Platform:Operator";

    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(PasswordHash);

    /// <summary>
    /// Constant-time-ish check of a submitted credential. The hash comparison is delegated
    /// to the same PBKDF2 hasher the tenant users use, so the stored format is identical.
    /// </summary>
    public bool Verify(string userName, string password)
    {
        if (!IsConfigured) return false;

        // Always verify the hash even when the name is wrong, so a wrong username and a
        // wrong password take the same time and cannot be told apart by timing.
        var nameMatches = string.Equals(userName?.Trim(), UserName.Trim(), StringComparison.OrdinalIgnoreCase);
        var hashMatches = new Pbkdf2PasswordHasher().Verify(password ?? string.Empty, PasswordHash);
        return nameMatches && hashMatches;
    }

    /// <summary>Generates a hash to paste into configuration. Used by the setup helper.</summary>
    public static string HashFor(string password) => new Pbkdf2PasswordHasher().Hash(password);
}

/// <summary>Names shared by the operator authentication scheme.</summary>
public static class PlatformAuth
{
    /// <summary>
    /// A second cookie scheme, separate from the tenant one. Two schemes rather than a
    /// claim on the tenant cookie means a signed-in tenant user can never be mistaken for
    /// the operator, and signing in as operator does not sign you into any tenant.
    /// </summary>
    public const string Scheme = "Platform";

    public const string Cookie = "gms.platform";
    public const string Policy = "platform:operator";
}
