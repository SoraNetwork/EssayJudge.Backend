using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SoraEssayJudge.Data;
using SoraEssayJudge.Dtos;
using SoraEssayJudge.Models;
using SoraEssayJudge.Services;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace SoraEssayJudge.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AuthController : ControllerBase
    {
        private readonly EssayContext _context;
        private readonly IConfiguration _config;

        private readonly IConfiguration _configuration;
        private readonly IDingTalkService _dingTalkService;

        public AuthController(EssayContext context, IConfiguration config, IDingTalkService dingTalkService)
        {
            _context = context;
            _config = config;
            _dingTalkService = dingTalkService;
            _configuration = config;
        }

	[AllowAnonymous]
        [HttpPost("dingtalk-login")]
        public async Task<IActionResult> DingTalkLogin([FromForm] string Code)
        {
            if (string.IsNullOrEmpty(Code))
            {
                return BadRequest("免登授权码不能为空");
            }

            try
            {
                var dingTalkUser = await _dingTalkService.GetLegacyUserInfoByCodeAsync(Code);

                var token = GenerateJwtToken(null, dingTalkUser.UnionId);
                return Ok(new LoginResponseDto
                {
                    Token = token,
                    User = new UserDto
                    {
                        Id = Guid.NewGuid(),
                        Username = dingTalkUser.Name,
                        Name = dingTalkUser.Name,
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"通过免登码获取钉钉用户信息失败: {ex.Message}");
            }
        }
	[AllowAnonymous]
        [HttpPost("dingtalk-sso-login")]
        public async Task<IActionResult> DingTalkSsoLogin([FromForm] string Code)
        {
            if (string.IsNullOrEmpty(Code))
            {
                return BadRequest("SSO 授权码不能为空");
            }

            try
            {
                var dingTalkUser = await _dingTalkService.GetSsoUserInfoByCodeAsync(Code);

                var token = GenerateJwtToken(null,dingTalkUser.UnionId);
                return Ok(new LoginResponseDto
                {
                    Token = token,
                    User = new UserDto
                    {
                        Id =Guid.NewGuid(),
                        Username = dingTalkUser.Nick,
                        Name = dingTalkUser.Nick,
                        PhoneNumber = dingTalkUser.Mobile
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"通过 SSO 授权码获取钉钉用户信息失败: {ex.Message}");
            }
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public IActionResult Register([FromForm] UserRegisterDto dto)
        {
            if (!_config.GetValue<bool>("Features:AllowUserRegistration"))
                return Forbid("注册功能已关闭");

            if (_context.Users.Any(u => u.Username == dto.Username))
                return BadRequest("用户名已存在");

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = dto.Username,
                Password = dto.Password, // 明文存储，生产环境请勿使用
                Name = dto.Name,
                PhoneNumber = dto.PhoneNumber,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            _context.SaveChanges();
            return Ok(new { message = "注册成功" });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public IActionResult Login([FromForm] UserLoginDto dto)
        {
            var user = _context.Users.FirstOrDefault(u => u.Username == dto.Username && u.Password == dto.Password);
            if (user == null)
                return Unauthorized("用户名或密码错误");

            var token = GenerateJwtToken(user);
            return Ok(new LoginResponseDto
            {
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Name = user.Name,
                    PhoneNumber = user.PhoneNumber,
                }
            });
        }

        private string GenerateJwtToken(User? user = null,String? UnionId = null)
        {
            if (user == null)
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = "DingTalkUser",
                    Name = "DingTalkUser",
                    Password = "none",
                    PhoneNumber = null
                };

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim("name", user.Name ?? ""),
                new Claim("phone", user.PhoneNumber ?? ""),
                new Claim("unionid", UnionId ?? "")
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
