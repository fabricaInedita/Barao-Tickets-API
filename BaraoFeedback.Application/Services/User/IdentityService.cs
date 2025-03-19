﻿using BaraoFeedback.Application.DTOs.Shared;
using BaraoFeedback.Application.DTOs.User;
using BaraoFeedback.Application.Services.User;
using BaraoFeedback.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options; 
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

public class IdentityService : IIdentityService
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor; 
    private readonly JwtOptions _jwtOptions;

    public IdentityService(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, IOptions<JwtOptions> jwtOptions, IHttpContextAccessor httpContextAccessor)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _jwtOptions = jwtOptions.Value;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<UserLoginResponse> LoginAsync(UserLoginRequest userLogin)
    {
        SignInResult signInResult = await _signInManager.PasswordSignInAsync(userLogin.UserName, userLogin.Password, isPersistent: false, lockoutOnFailure: true);
  
        if (signInResult.Succeeded)
        {
            var credenciais = await GenerateCredentials(userLogin.UserName);
            return credenciais;
        }

        UserLoginResponse userLoginResponse = new UserLoginResponse(signInResult.Succeeded);
        if (!signInResult.Succeeded)
        {
            if (signInResult.IsLockedOut)
            {
                userLoginResponse.Errors.AddError("Esta conta está bloqueada.");
            }
            else if (signInResult.IsNotAllowed)
            {
                userLoginResponse.Errors.AddError("Esta conta não tem permissão para entrar.");
            }
            else if (signInResult.RequiresTwoFactor)
            {
                userLoginResponse.Errors.AddError("Confirme seu email.");
            }
            else
            {
                userLoginResponse.Errors.AddError("Nome de usuário ou senha estão incorretos.");
            }
        }

        return userLoginResponse;
    }
    public async Task<UserRegisterResponse> RegisterAdminAsync(string type, AdminRegisterRequest request)
    {
        var user = new ApplicationUser()
        {
            Email = request.Email,
            Type = type,
            Name = request.Name,
            UserName = request.Email,
        };

        var password = GeneratePassword();
        IdentityResult result = await _userManager.CreateAsync(user, password);

        var response = await ValidateRegisterAsync(result);

        if (!response.Success)
            return response;

        response.Data = password;

        return response;
    }
    public async Task<DefaultResponse> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);

        await _userManager.DeleteAsync(user);

        return new();
    }
    public async Task<UserRegisterResponse> RegisterStudentAsync(string type, StudentRegisterRequest userRegister)
    {
        string email = userRegister.StudentCode + "@baraodemaua.edu.br";
        var user = new ApplicationUser()
        {
            Email = email,
            Type = type,
            Name = userRegister.Name,
            UserName = userRegister.StudentCode,
        };         

        IdentityResult result = await _userManager.CreateAsync(user, userRegister.Password);

        return await ValidateRegisterAsync(result);
    }

    public async Task<DefaultResponse> GetUsers()
    {
        var users = _userManager.Users
            .Where(x => x.Type == "admin")
            .Select(u => new { u.Id, u.UserName, u.Name, u.Email })
            .ToList();

        var response = new DefaultResponse();

        response.Data = users;

        return response;
    }
    public async Task<DefaultResponse> UpdatePasswordAsync(UpdatePassword dto)
    {
        var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
 
        var user = await _userManager.FindByIdAsync(userId); 
        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.Password); 

        return new DefaultResponse();
    }

    private async Task<UserRegisterResponse> ValidateRegisterAsync(IdentityResult result)
    {

        UserRegisterResponse userRegisterResponse = new UserRegisterResponse(result.Succeeded);

        if (!result.Succeeded)
        {
            foreach (var erroAtual in result.Errors)
            {
                switch (erroAtual.Code)
                {
                    case "PasswordRequiresNonAlphanumeric":
                        userRegisterResponse.Errors.AddError("A senha precisa conter pelo menos um caracter especial - ex( * | ! ).");
                        break;

                    case "PasswordRequiresDigit":
                        userRegisterResponse.Errors.AddError("A senha precisa conter pelo menos um número (0 - 9).");
                        break;

                    case "PasswordRequiresUpper":
                        userRegisterResponse.Errors.AddError("A senha precisa conter pelo menos um caracter em maiúsculo.");
                        break;

                    case "DuplicateUserName":
                        userRegisterResponse.Errors.AddError("O email informado já foi cadastrado!");
                        break;

                    default:
                        userRegisterResponse.Errors.AddError("Erro ao criar usuário.");
                        break;
                }

            }
        }

        return userRegisterResponse;
    }
    public async Task<IEnumerable<ApplicationUser>> GetUser()
    {
        var result = _userManager.Users.AsEnumerable();

        return result;

    } 

    protected async Task<UserLoginResponse> GenerateCredentials(string username)
    {
        var user = await _userManager.FindByNameAsync(username);
        var accessTokenClaims = await GetClaims(user, adicionarClaimsUsuario: true);
        var refreshTokenClaims = await GetClaims(user, adicionarClaimsUsuario: false);

        var dataExpiracaoAccessToken = DateTime.Now.AddSeconds(_jwtOptions.AccessTokenExpiration);
        var dataExpiracaoRefreshToken = DateTime.Now.AddSeconds(10800);

        var accessToken = GenerateToken(accessTokenClaims, dataExpiracaoAccessToken);
        var refreshToken = GenerateToken(refreshTokenClaims, dataExpiracaoRefreshToken);
        var expirationAcessToken = _jwtOptions.AccessTokenExpiration.ToString();
        var expirationTimeRefreshToken = _jwtOptions.RefreshTokenExpiration.ToString();

        return new UserLoginResponse
        (
            true,
            user.Type,
            accessToken, 
            refreshToken,
            expirationTimeRefreshToken,
            expirationAcessToken,
            user.Name,
            user.Id,
            user.Email
        );
    }

    protected string GenerateToken(IEnumerable<Claim> claims, DateTime dataExpiracao)
    {
        JwtSecurityToken token = new JwtSecurityToken(_jwtOptions.Issuer, _jwtOptions.Audience, claims, DateTime.Now, expires: dataExpiracao, signingCredentials: _jwtOptions.SigningCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    private async Task<IList<Claim>> GetClaims(ApplicationUser user, bool adicionarClaimsUsuario)
    {
        var claims = await _userManager.GetClaimsAsync(user);

        var roles = await _userManager.GetRolesAsync(user);

        claims.Add(new Claim(JwtRegisteredClaimNames.Sub, user.Id));
        claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));

        var now = DateTimeOffset.UtcNow;
        claims.Add(new Claim(JwtRegisteredClaimNames.Nbf, now.ToUnixTimeSeconds().ToString()));
        claims.Add(new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return claims;
    }

    public static string GeneratePassword()
    {
        var Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        char[] password = new char[6];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            byte[] randomBytes = new byte[6];
            rng.GetBytes(randomBytes);

            for (int i = 0; i < password.Length; i++)
            {
                int index = randomBytes[i] % Characters.Length;
                password[i] = Characters[index];
            }
        }
        return new string(password);
    }


}