namespace WeatherApi.Auth;

// Contract for checking credentials. Swap the implementation (database, identity
// provider) without touching the login endpoint.
public interface IUserValidator
{
    bool IsValid(string username, string password);
}
