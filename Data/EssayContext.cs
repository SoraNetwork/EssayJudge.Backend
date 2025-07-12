using Microsoft.EntityFrameworkCore;
using SoraEssayJudge.Models;

namespace SoraEssayJudge.Data
{
    public class EssayContext : DbContext
    {
        public EssayContext(DbContextOptions<EssayContext> options) : base(options)
        {
        }

        public DbSet<EssayAssignment> EssayAssignments { get; set; }
        public DbSet<EssaySubmission> EssaySubmissions { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<AIResult> AIResults { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Class> Classes { get; set; }
        public DbSet<ApiKey> ApiKeys { get; set; }
        public DbSet<AIModel> AIModels { get; set; }
        public DbSet<AIModelUsageSetting> AIModelUsageSettings { get; set; }
    }
}