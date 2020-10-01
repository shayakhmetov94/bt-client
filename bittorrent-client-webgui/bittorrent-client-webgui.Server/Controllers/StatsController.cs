using System.Linq;
using Microsoft.AspNetCore.Mvc;
using bittorrent_client_webgui.Shared;
using System.Data;
using bittorrent_service.Base.Db;

namespace bittorrent_client_webgui.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StatsController : ControllerBase {
        private IBtServiceDataContext _dataContext;
        public StatsController(IBtServiceDataContext dataContext) {
            _dataContext = dataContext;
        }
    
        [Route("")]
        [HttpGet]
        public ServerStats Index() {
            var stats = _dataContext.GetStats();
            return new ServerStats
            {
                SessionsCount = stats.SessionCount,
                ActiveSessionsCount = stats.ActiveSessionCount,
                AverageDownloadSpeed = _dataContext.AverageDownloadSpeed()
                    .Select((s) => new SpeedStamp()
                    {
                        UtcTime = s.UtcTime,
                        Value = s.Value
                    }
                ),
                AverageUploadSpeed = _dataContext.AverageUploadSpeed()
                    .Select((s) => new SpeedStamp()
                    {
                        UtcTime = s.UtcTime,
                        Value = s.Value
                    }
                )
            };
            

        }
    }
}

