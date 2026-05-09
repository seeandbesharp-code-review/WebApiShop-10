namespace Services;
using AutoMapper;
using DTOs;
using Entities;
using Repository;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly IMapper _mapper;
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration;

    public UserService(IUserRepository userRepository, IPasswordService passwordService, IMapper mapper, IDistributedCache cache, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _mapper = mapper;
        _cache = cache;
        _configuration = configuration;
    }

    public async Task<UserDTO> GetUserById(int id)
    {
        string cacheKey = $"user_{id}";

        try
        {
            var cachedUser = await _cache.GetStringAsync(cacheKey);
            if (cachedUser != null)
            {
                return JsonSerializer.Deserialize<UserDTO>(cachedUser);
            }
        }
        catch (Exception ex)
        {
            // Redis למטה? לא נורא, נדפיס הודעה ונמשיך ל-DB
            Console.WriteLine($"Redis error on Get: {ex.Message}");
        }

        var user = _mapper.Map<User, UserDTO>(await _userRepository.GetUserById(id));

        if (user != null)
        {
            await SetCacheAsync(cacheKey, user);
        }

        return user;
    }

    public async Task<ResultValidUser<(UserDTO user, string token)>> AddUser(UserWithPasswordDTO user)
    {
        if (!IsValidEmail(user.UserEmail))
            return new ResultValidUser<(UserDTO, string)>(false, false, true, (null, null));
        Password passwordAfterCheck = _passwordService.CheckPassword(user.UserPassword);
        if (passwordAfterCheck.Level < 3)
            return new ResultValidUser<(UserDTO, string)>(true, false, false, (null, null));
        if (await EmailExists(user.UserEmail, user.UserId))
            return new ResultValidUser<(UserDTO, string)>(false, true, false, (null, null));
        User user1 = _mapper.Map<UserWithPasswordDTO, User>(user);
        user1.Password = user.UserPassword;
        UserDTO user2 = _mapper.Map<User, UserDTO>(await _userRepository.AddUser(user1));
        await InvalidateUserCache(user2.UserId);
        string token = GenerateToken(user2);
        return new ResultValidUser<(UserDTO, string)>(false, false, false, (user2, token));
    }
    public async Task<ResultValidUser<bool>> UpdateUser(int id, UserWithPasswordDTO user)
    {
        if (!IsValidEmail(user.UserEmail))
            return new ResultValidUser<bool>(false, false, true, false);
        Password passwordAfterCheck = _passwordService.CheckPassword(user.UserPassword);
        if (passwordAfterCheck.Level < 3)
        {
            return new ResultValidUser<bool>(true, false,false, false);
        }
        else if (await EmailExists(user.UserEmail, id))
        {
            return new ResultValidUser<bool>(false, true,false, false);
        }
        else
        {
            User user1 = _mapper.Map<UserWithPasswordDTO, User>(user);
            user1.UserId = id;
            user1.Password = user.UserPassword;
            await _userRepository.UpdateUser(user1);
            await InvalidateUserCache(id);
            return new ResultValidUser<bool>(false, false, false, true);
        }
    }
    public async Task<(UserDTO user, string token)> Login(LoginUserDTO loginUser)
    {
        UserDTO user = _mapper.Map<User, UserDTO>(await _userRepository.Login(loginUser.UserEmail, loginUser.UserPassword));
        if (user == null) return (null, null);
        string token = GenerateToken(user);
        return (user, token);
    }
    
    public async Task<bool> EmailExists(string email,int id)
    {
        User user = await _userRepository.GetUserByEmail(email);
        if (user != null && user.UserId!=id)
        {
            return true;
        }
        return false;
    }

    public bool IsValidEmail(string email)
    {
        return new EmailAddressAttribute().IsValid(email);
    }

    private string GenerateToken(UserDTO user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.UserEmail),
            new Claim(ClaimTypes.GivenName, user.UserFirstName),
            new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
        };
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task InvalidateUserCache(int userId)
    {
        try
        {
            string cacheKey = $"user_{userId}";
            await _cache.RemoveAsync(cacheKey);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis error on Invalidate: {ex.Message}");
        }
    }

    private async Task SetCacheAsync(string cacheKey, UserDTO user)
    {
        try
        {
            var ttlString = _configuration["Redis:TTL"];
            var ttl = string.IsNullOrEmpty(ttlString) ? 3600 : int.Parse(ttlString);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttl)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(user), options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis error on Set: {ex.Message}");
        }
    }
}
