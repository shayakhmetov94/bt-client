using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using bittorrent_service.Base.Db;
using bittorrent_service.Models;
using Microsoft.AspNetCore.Mvc;

namespace bittorrent_client_webgui.Server.Controllers
{
    [Route("[controller]")]
    [Controller]
    public class SessionsController : ControllerBase
    {
        IBtServiceDataContext _dataContext;

        public SessionsController(IBtServiceDataContext dataContext) {
            _dataContext = dataContext;
        }

        [HttpGet]
        public IEnumerable<TorrentSession> Index() {
            return _dataContext.ListSessions(null, 10, null);
        }
    }
}