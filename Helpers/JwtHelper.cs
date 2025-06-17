using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Z_TRIP.Models;

namespace Z_TRIP.Helpers
{
    public class JwtHelper
    {
        private readonly IConfiguration _config;

        public JwtHelper(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateJwtToken(Users user)
        {
            // Dapatkan kunci rahasia JWT
            var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key not found in configuration");

            // Buat claims untuk token
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, user.Role ? "Admin" : "User"),
                new Claim("userId", user.Id.ToString()),
                new Claim("name", user.Name)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Token expires dalam 1 hari
            var expires = DateTime.Now.AddDays(7);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public JwtSecurityToken ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key not found in configuration"));

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"],
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return (JwtSecurityToken)validatedToken;
        }

        public int GetUserIdFromToken(string token)
        {
            try
            {
                var jwtToken = ValidateToken(token);
                var userId = jwtToken.Claims.First(x => x.Type == "userId").Value;
                return int.Parse(userId);
            }
            catch
            {
                return 0;
            }
        }

        public bool IsAdmin(string token)
        {
            try
            {
                var jwtToken = ValidateToken(token);
                var role = jwtToken.Claims.First(x => x.Type == ClaimTypes.Role).Value;
                return role == "Admin";
            }
            catch
            {
                return false;
            }
        }
    }
}
