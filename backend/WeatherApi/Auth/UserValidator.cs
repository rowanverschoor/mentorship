using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace WeatherApi.Auth;

// Contract for checking credentials. Swap the implementation (database, identity
// provider) without touching the login endpoint.
public interface IUserValidator
{
    bool IsValid(string username, string password);
}

// Single configured user. Fixed-time compare so response time leaks nothing about the password.
public class ConfiguredUserValidator(IOptions<AuthOptions> options) : IUserValidator
{
    private readonly AuthOptions _auth = options.Value;

    public bool IsValid(string username, string password) =>
        Equal(username, _auth.User) & Equal(password, _auth.Password);

    private static bool Equal(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
