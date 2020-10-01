using bittorrent_client.Base.Peers;
using commons_bittorrent.Base.Bencoding;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;

namespace bittorrent_client.Base.Announce
{
    public class Announcer {

        public enum ClientEvent { Started, Stopped, Completed, None }

        #region Props and constants

        private static readonly string RS_INTERVAL      = "interval";
        private static readonly string RS_MININTERVAL   = "min interval";
        private static readonly string RS_TRACKERID     = "tracker id";
        private static readonly string RS_COMPLETE      = "complete";
        private static readonly string RS_INCOMPLETE    = "incomplete";
        private static readonly string RS_PEERS         = "peers";
        private static readonly string RS_FAILURE       = "failure reason";
        private static readonly string RS_WARNING       = "warning message";

        /// <summary>
        /// Client's port. Required for tracker request
        /// </summary>
        private int _port;

        /// <summary>
        /// Optional request field, not sure yet how it used
        /// </summary>
        private string _trackerId;

        /// <summary>
        /// An additional identification key of client
        /// </summary>
        private string _key;

        /// <summary>
        /// BEncoder
        /// </summary>
        public Bencoder Bencoder { get; private set; } = new Bencoder();

        /// <summary>
        /// User-Agent for HTTP query request
        /// </summary>
        private static readonly string UserAgent = "TestTorrent";

        /// <summary>
        /// Tracker's URL
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Seeders count from latest announce query
        /// </summary>
        public int Seeders { get; private set; }

        /// <summary>
        /// Leechers count from latest tracker query
        /// </summary>
        public int Leechers { get; private set; }

        /// <summary>
        /// Update interval requested by tracker
        /// </summary>
        public int RegularInterval { get; private set; } = 500;

        /// <summary>
        /// Re-announce interval requested by tracker
        /// </summary>
        public int ReAnnounceInterval { get; private set; } = 500;

        /// <summary>
        /// Last re-announce date in UTC timezone
        /// </summary>
        public DateTime LastReAnnounceDateUtc { get; private set; }

        public IBtClient Client { get; private set; }
        #endregion

        public Announcer(IBtClient btClient, string announceUrl, string trackerId = null, string key = null) {
            Url         = announceUrl;
            Client      = btClient;
            _trackerId  = trackerId;
            _key        = key;
            _port       = btClient.GetEndPoint().Port;
        }

        /// <summary>
        /// Requests peer and other info from tracker
        /// </summary>
        /// <param name="evnt">Client event</param>
        /// <param name="compact">Whether tracker response should be compact</param>
        /// <returns>Tracker response</returns>
        private Dictionary<string, object> QueryTracker(ClientEvent evnt = ClientEvent.None, bool compact = true) {
            BtClientStats clientStats = Client.GetStats();

            NameValueCollection httpRqArgs = new NameValueCollection();
            httpRqArgs.Add("info_hash",     HttpUtility.UrlEncode(Client.GetInfoHash()));
            httpRqArgs.Add("peer_id",       HttpUtility.UrlEncode(Client.GetPeerID()));
            httpRqArgs.Add("uploaded",      clientStats.Uploaded.ToString());
            httpRqArgs.Add("downloaded",    clientStats.Downloaded.ToString());
            httpRqArgs.Add("left",          clientStats.Left.ToString());
            httpRqArgs.Add("compact",       compact ? "1" : "0");
            httpRqArgs.Add("event",         evnt.ToString());

            if(_trackerId != null) {
                httpRqArgs.Add("trackerid", _trackerId);
            }

            if (_key != null) {
                httpRqArgs.Add("key", _key);
            }

            string rqUrl = Url + ToQueryString(httpRqArgs);

            Debug.WriteLine($"Tracker request: {rqUrl}");
            HttpWebRequest httpRq = (HttpWebRequest)WebRequest.Create(rqUrl);
            httpRq.UserAgent = UserAgent;

            HttpWebResponse httpRs = (HttpWebResponse)httpRq.GetResponse();
            if (httpRs.StatusCode == HttpStatusCode.OK) {
                MemoryStream httpRsStream = new MemoryStream(httpRs.ContentLength != -1 ? (int)httpRs.ContentLength : 0);
                httpRs.GetResponseStream()?.CopyTo(httpRsStream);
                return (Dictionary<string, object>)Bencoder.DecodeElement(httpRsStream.ToArray());
            }

            return null;
        }

        public bool Query(out List<Peers.Peer> peers, ClientEvent clientEvent = ClientEvent.None) {
            if (clientEvent == ClientEvent.Started) {
                LastReAnnounceDateUtc = DateTime.UtcNow;
            }
            peers = null;

            Dictionary<string, object> trackerRs = null;
            try {
                trackerRs = QueryTracker(clientEvent);
            } catch (WebException we) {
                Trace.WriteLine($"Exception while querying tracker {Url}: {we}");
                return false;
            }

            if (trackerRs.ContainsKey(RS_FAILURE)) {
                Trace.WriteLine($"Failure from tracker {Url}: {Bencoder.Encoding.GetString((byte[])trackerRs[RS_FAILURE])}");
                return false;
            }

            if (trackerRs.ContainsKey(RS_WARNING)) {
                Trace.WriteLine($"Warning from tracker {Url}: {trackerRs[RS_WARNING]}");
            }

            object rsVal;
            if (trackerRs.TryGetValue(RS_INTERVAL, out rsVal)) {
                int rsRegInterval = (int)rsVal;

                // Cheking whether if there are interval changes between queries 
                if (RegularInterval != 0 && RegularInterval != rsRegInterval) {
                    Trace.WriteLine($"Query interval of tracker {Url} has changed! Was {RegularInterval} now {rsRegInterval}");
                }

                RegularInterval = rsRegInterval;
            }

            if (trackerRs.TryGetValue(RS_MININTERVAL, out rsVal)) {
                ReAnnounceInterval = (int)rsVal;
            }

            if (trackerRs.TryGetValue(RS_TRACKERID, out rsVal)) {
                _trackerId = (string)rsVal;
            }

            if (trackerRs.TryGetValue(RS_COMPLETE, out rsVal)) {
                Seeders = (int)rsVal;
            }

            if (trackerRs.TryGetValue(RS_INCOMPLETE, out rsVal)) {
                Leechers = (int)rsVal;
            }

            object respPeers;
            if (!trackerRs.TryGetValue(RS_PEERS, out respPeers)) {
                return false;
            }

            if (respPeers is String) {
                Debug.WriteLine($"No peers form announce {Url}");
                return false;
            }

            //Parse peers
            List<Peer> rsPeers = new List<Peer>();
            if (respPeers is byte[]) {
                byte[] trackerRsPeers = (byte[])respPeers;
                int offset = 0;
                while (offset < trackerRsPeers.Length) {
                    byte[] peerInfo = new byte[6];
                    Buffer.BlockCopy(trackerRsPeers, offset, peerInfo, 0, 6);
                    offset += 6;
                    rsPeers.Add(new Peer(peerInfo));
                }
            } else {
                List<object> rsPeersList = (List<object>)respPeers;
                for (int i = 0; i < rsPeersList.Count; i++) {
                    Dictionary<string, object> peerInfo = (Dictionary<string, object>)rsPeersList[i];

                    rsPeers.Add(
                        new Peer(
                            (string)peerInfo["ip"],
                            (int)peerInfo["port"],
                            (string)peerInfo["peer id"]
                        )
                    );
                }
            }

            peers = rsPeers;
            return true;
        }

        private string ToQueryString(NameValueCollection nvc) {
            var array = (from key in nvc.AllKeys
                         from value in nvc.GetValues(key)
                         select string.Format("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(value)))
                .ToArray();
            return "?" + string.Join("&", array);
        }

        public override bool Equals( object obj ) {
            if (!(obj is Announcer other)) {
                return false;
            }

            return other.Url.Equals( this.Url );
        }

        public override int GetHashCode() {
            return Url.GetHashCode();
        }
    }
}
