using System.Threading.Tasks;
using SoraEssayJudge.Models;

namespace SoraEssayJudge.Services
{
    public interface IDingTalkService
    {
        Task<string> GetAccessTokenAsync(); // This gets the app access token
        Task<DingTalkUserInfo> GetLegacyUserInfoByCodeAsync(string code); // For in-app免登
        Task<DingTalkContactUser> GetSsoUserInfoByCodeAsync(string ssoCode); // For web SSO
    }
}
