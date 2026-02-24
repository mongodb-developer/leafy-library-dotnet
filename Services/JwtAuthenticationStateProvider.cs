using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Leafy_Library.Services;

/// <summary>
/// Custom AuthenticationStateProvider for Blazor Server.
/// Stores the JWT in browser localStorage (via ProtectedBrowserStorage)
/// and validates it to provide auth state to the app — mirroring how the
/// Angular client stored tokens in localStorage and used angular-jwt.
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ProtectedLocalStorage _localStorage;
    private readonly TokenService _tokenService;
    private ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public JwtAuthenticationStateProvider(
        ProtectedLocalStorage localStorage,
        TokenService tokenService)
    {
        _localStorage = localStorage;
        _tokenService = tokenService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var tokenResult = await _localStorage.GetAsync<string>("access_token");
            if (!tokenResult.Success || string.IsNullOrEmpty(tokenResult.Value))
            {
                return new AuthenticationState(_anonymous);
            }

            var principal = _tokenService.ValidateToken(tokenResult.Value);
            if (principal is null)
            {
                return new AuthenticationState(_anonymous);
            }

            return new AuthenticationState(principal);
        }
        catch
        {
            return new AuthenticationState(_anonymous);
        }
    }

    public async Task LoginAsync(string token)
    {
        await _localStorage.SetAsync("access_token", token);
        var principal = _tokenService.ValidateToken(token);
        NotifyAuthenticationStateChanged(
            Task.FromResult(new AuthenticationState(principal ?? _anonymous)));
    }

    public async Task LogoutAsync()
    {
        await _localStorage.DeleteAsync("access_token");
        NotifyAuthenticationStateChanged(
            Task.FromResult(new AuthenticationState(_anonymous)));
    }
}
