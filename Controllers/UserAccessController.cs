using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SoraEssayJudge.Data;
using SoraEssayJudge.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;

namespace SoraEssayJudge.Controllers
{
    public class UserAccessController
    {
        /// <summary>
        /// 检查用户是否有足够的权限访问
        /// </summary>
        /// <param name="token">JWT令牌</param>
        /// <param name="permissionLevel">权限等级</param>
        /// <param name="context">数据库上下文</param>
        /// <param name="config">配置</param>
        /// <returns>是否满足权限</returns>
        public static bool CheckAccess(string token, int permissionLevel, EssayContext context, IConfiguration config)
        {
            try
            {
                // 解码JWT令牌
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = config["Jwt:Audience"],
                    ClockSkew = TimeSpan.Zero,
                    ValidateLifetime = true
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);

                // 输出所有Claims用于调试
                Console.WriteLine($"[CheckAccess] JWT中的所有Claims:");
                foreach (var claim in principal.Claims)
                {
                    Console.WriteLine($"  Type: {claim.Type}, Value: {claim.Value}");
                }

                // 优先使用 unique_name，兼容旧令牌使用 name claim
                var usernameClaim = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.UniqueName)
                                   ?? principal.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");

                if (usernameClaim == null)
                {
                    Console.WriteLine($"[CheckAccess] 用户名Claim未找到");
                    return false;
                }

                var username = usernameClaim.Value;
                Console.WriteLine($"[CheckAccess] 用户名: {username}, 需要权限等级: {permissionLevel}");

                // 从数据库获取用户的Role
                var user = context.Users.FirstOrDefault(u => u.Username == username);
                if (user == null)
                {
                    Console.WriteLine($"[CheckAccess] 用户不存在: {username}");
                    return false;
                }

                if (string.IsNullOrEmpty(user.Role))
                {
                    Console.WriteLine($"[CheckAccess] 用户Role为空: {username}");
                    return false;
                }

                Console.WriteLine($"[CheckAccess] 用户Role: {user.Role}");

                // 从Roles表获取Role对应的Id
                var role = context.Roles.FirstOrDefault(r => r.Name == user.Role);
                if (role == null)
                {
                    Console.WriteLine($"[CheckAccess] Role不存在于Roles表: {user.Role}");
                    return false;
                }

                Console.WriteLine($"[CheckAccess] Role Id: {role.Id}");

                // 检查权限等级
                bool hasAccess = role.Id <= permissionLevel;
                Console.WriteLine($"[CheckAccess] 权限检查结果: {hasAccess} (RoleId={role.Id} <= PermissionLevel={permissionLevel})");
                return hasAccess;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CheckAccess] 异常: {ex.Message}");
                return false;
            }
        }
    }
}