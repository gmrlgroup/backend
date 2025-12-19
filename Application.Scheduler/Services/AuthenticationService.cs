using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Application.Scheduler.Services;


public class AuthenticationService
{
    private readonly string _aadTenant;
    private readonly string _aadResource;
    private readonly string _aadClientAppId;
    private readonly string _aadClientAppSecret;

    public AuthenticationService(string aadTenant, string aadResource, string aadClientAppId, string aadClientAppSecret)
    {
        _aadTenant = aadTenant;
        _aadResource = aadResource;
        _aadClientAppId = aadClientAppId;
        _aadClientAppSecret = aadClientAppSecret;
    }


    public string GetAuthenticationHeader()
    {
        AuthenticationContext authenticationContext = new AuthenticationContext(_aadTenant);
        
        var creadential = new ClientCredential(_aadClientAppId, _aadClientAppSecret);
        
        AuthenticationResult authenticationResult = authenticationContext.AcquireTokenAsync(_aadResource, creadential).Result;
    	
        return authenticationResult.AccessToken;
    }
}