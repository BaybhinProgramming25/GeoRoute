using System.IdentityModel.Tokens.Jwt;                                                                                  
using System.Security.Claims;         
using System.Text;                                                                                                      
using Microsoft.IdentityModel.Tokens;
                                    
public class TokenService {
    private readonly IConfiguration _config;
                                            
    public TokenService(IConfiguration config) {                                                                        
        _config = config;                       
    }                                                                                                                   
                
    public string GenerateToken(string userId, string email) {
        
        var claims = new[] {                                  
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email)                                                                          
        };                                    
                                                                                                                        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);                                         
                                                                                
        var token = new JwtSecurityToken(                                                                               
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,                                                                                             
            expires: DateTime.Now.AddMinutes(double.Parse(_config["Jwt:ExpiryMinutes"]!)),
            signingCredentials: creds                                                                                   
        );                                                                                                              

        return new JwtSecurityTokenHandler().WriteToken(token);                                                         
    }           
}