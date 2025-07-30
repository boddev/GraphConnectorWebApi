using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ApiGraphActivator.Models.Mcp;
using Microsoft.Graph;
using Microsoft.IdentityModel.Tokens;

namespace ApiGraphActivator.Services.Mcp;

/// <summary>
/// Azure AD authentication provider for MCP sessions
/// </summary>
public class AzureAdSessionAuthenticationProvider : ISessionAuthenticationProvider
{
    private readonly ILogger<AzureAdSessionAuthenticationProvider> _logger;
    private readonly IConfiguration _configuration;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public AzureAdSessionAuthenticationProvider(
        ILogger<AzureAdSessionAuthenticationProvider> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<McpAuthenticationInfo?> AuthenticateWithAzureAdAsync(string accessToken)
    {
        try
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Access token is null or empty");
                return null;
            }

            // Validate token format
            if (!_tokenHandler.CanReadToken(accessToken))
            {
                _logger.LogWarning("Invalid token format");
                return null;
            }

            var token = _tokenHandler.ReadJwtToken(accessToken);
            
            // Extract basic information from token
            var claims = await ExtractClaimsAsync(accessToken);
            
            // Check if token is expired
            var expiresAt = token.ValidTo;
            if (DateTime.UtcNow >= expiresAt)
            {
                _logger.LogWarning("Token has expired");
                return null;
            }

            _logger.LogInformation("Successfully authenticated user {UserId}", 
                claims.GetValueOrDefault("sub", "unknown"));

            return new McpAuthenticationInfo
            {
                AuthenticationType = "AzureAD",
                AccessToken = accessToken,
                TokenExpiresAt = expiresAt,
                Claims = claims,
                IsAuthenticated = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating with Azure AD");
            return null;
        }
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            if (string.IsNullOrEmpty(token) || !_tokenHandler.CanReadToken(token))
            {
                return false;
            }

            var jwtToken = _tokenHandler.ReadJwtToken(token);
            
            // Check expiration
            if (DateTime.UtcNow >= jwtToken.ValidTo)
            {
                _logger.LogDebug("Token validation failed: token expired");
                return false;
            }

            // Check issuer if configured
            var expectedIssuer = _configuration["AzureAd:TenantId"];
            if (!string.IsNullOrEmpty(expectedIssuer))
            {
                var issuer = jwtToken.Claims.FirstOrDefault(c => c.Type == "iss")?.Value;
                if (!string.IsNullOrEmpty(issuer) && !issuer.Contains(expectedIssuer))
                {
                    _logger.LogDebug("Token validation failed: invalid issuer");
                    return false;
                }
            }

            // Check audience if configured
            var expectedClientId = _configuration["AzureAd:ClientId"];
            if (!string.IsNullOrEmpty(expectedClientId))
            {
                var audience = jwtToken.Claims.FirstOrDefault(c => c.Type == "aud")?.Value;
                if (!string.IsNullOrEmpty(audience) && audience != expectedClientId)
                {
                    _logger.LogDebug("Token validation failed: invalid audience");
                    return false;
                }
            }

            await Task.CompletedTask; // Make async
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return false;
        }
    }

    public async Task<McpAuthenticationInfo?> RefreshTokenAsync(string refreshToken)
    {
        await Task.CompletedTask; // Make async
        
        // Note: In a real implementation, you would call Azure AD token endpoint
        // to refresh the token. For this demo, we'll return null to indicate
        // that refresh is not supported in this simple implementation.
        
        _logger.LogWarning("Token refresh not implemented in this demo version");
        return null;
    }

    public async Task<Dictionary<string, string>> ExtractClaimsAsync(string token)
    {
        var claims = new Dictionary<string, string>();
        
        try
        {
            if (string.IsNullOrEmpty(token) || !_tokenHandler.CanReadToken(token))
            {
                return claims;
            }

            var jwtToken = _tokenHandler.ReadJwtToken(token);
            
            foreach (var claim in jwtToken.Claims)
            {
                // Only include standard and relevant claims
                if (IsRelevantClaim(claim.Type))
                {
                    claims[claim.Type] = claim.Value;
                }
            }

            await Task.CompletedTask; // Make async
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting claims from token");
        }

        return claims;
    }

    private bool IsRelevantClaim(string claimType)
    {
        var relevantClaims = new[]
        {
            ClaimTypes.NameIdentifier,
            ClaimTypes.Name,
            ClaimTypes.Email,
            ClaimTypes.Role,
            "sub",
            "name",
            "email", 
            "preferred_username",
            "given_name",
            "family_name",
            "tid", // tenant id
            "oid", // object id
            "upn", // user principal name
            "scp", // scope
            "roles"
        };

        return relevantClaims.Contains(claimType);
    }
}