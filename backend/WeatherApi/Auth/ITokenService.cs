namespace WeatherApi.Auth;

// Contract for issuing a token. The login endpoint depends on this, not on JWT details.
public interface ITokenService
{
    string Create(string username);
}
