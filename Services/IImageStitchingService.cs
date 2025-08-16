using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SoraEssayJudge.Services
{
    public interface IImageStitchingService
    {
        Task<string> StitchImagesAsync(IEnumerable<Stream> imageStreams, string uploadPath, int spacing = 20);
    }
}
