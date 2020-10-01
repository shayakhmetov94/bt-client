using bittorrent_client.Base.Strategies;
using bittorrent_client.Base.Peers;
using FluentScheduler;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace bittorrent_client.Base.Announce
{
    public class AnnounceManager : IResourcePool<Peers.Peer>, IDisposable
    {
        /// <summary>
        /// FluentScheduler schedule name
        /// </summary>
        private static readonly string AnnounceUpdateScheduleName = "AnnounceUpdate";

        /// <summary>
        /// Current peers in announcer manager
        /// </summary>
        private SortedSet<IPAddress> _peersHave;

        /// <summary>
        /// Announcers
        /// </summary>
        private List<Announcer> _announcers;

        /// <summary>
        /// Peers pool
        /// </summary>
        private BlockingCollection<Peers.Peer> _myPool;

        public AnnounceManager(BtClient client, List<string> announcerUrls) {
            if(announcerUrls == null) {
                throw new ArgumentNullException("announces");
            }

            _announcers = new List<Announcer>(1);
            foreach(var announcerUrl in announcerUrls) {
                _announcers.Add(new Announcer(client, announcerUrl));
            }

            _peersHave = new SortedSet<IPAddress>();
            _myPool = new BlockingCollection<Peers.Peer>();
        }

        /// <summary>
        /// Schedules announce updates
        /// </summary>
        public void Start() {
            InitAnnounces();
            ScheduleAnnounceUpdates();
        }

        /// <summary>
        /// Take peer from tracker(s)
        /// </summary>
        /// <returns>Peer info</returns>
        public Peer Acquire() {
            return _myPool.Take();
        }

        /// <summary>
        /// Return peer back to pool
        /// </summary>
        /// <param name="resource">Peer info</param>
        public void Realese(Peers.Peer resource ) {
            _myPool.Add( resource );
        }

        /// <summary>
        /// Tells to tracker(s) that client started download
        /// </summary>
        private void InitAnnounces() {
            foreach(var announcer in _announcers) {
                if(announcer.Query(out List<Peers.Peer> peers, Announcer.ClientEvent.Started)) {
                    ProcessAnnouncerPeers(peers);
                }
            }
        }

        /// <summary>
        /// Tells to tracker(s) that client stopped downloading
        /// </summary>
        private void StopAnnounces() {
            foreach(var announcer in _announcers) {
                announcer.Query(out List<Peers.Peer> peers, Announcer.ClientEvent.Stopped);
            }
        }

        /// <summary>
        /// Create schedules for peer updates from trackers
        /// </summary>
        private void ScheduleAnnounceUpdates() {
            foreach(var announce in _announcers) {
                JobManager.AddJob(new AnnounceUpdateJob(this, announce),
                    (schedule) => schedule.WithName(AnnounceUpdateScheduleName)
                        .ToRunEvery(announce.RegularInterval)
                        .Seconds()
                );
            }
        }

        /// <summary>
        /// Unschedules peer updates
        /// </summary>
        private void UnscheduleAnnounceUpdates() {
            foreach(var schedule in JobManager.AllSchedules) {
                if(schedule.Name == AnnounceUpdateScheduleName) {
                    schedule.Disable();
                }
            }
        }

        /// <summary>
        /// Processes new peers from tracker
        /// </summary>
        /// <param name="peers">New peers</param>
        private void ProcessAnnouncerPeers(List<Peers.Peer> peers) {
            lock (_peersHave) {
                foreach (var peer in peers) {
                    if (!_peersHave.Contains(peer.IpAddress)) { 
                        _myPool.Add(peer);
                        _peersHave.Add(peer.IpAddress);
                        Debug.WriteLine($"{peer.IpAddress}: {peer.Port}");
                    } else {
                        Debug.WriteLine($"AnnounceManager: peer {peer.IpAddress} already in pool");
                    }
                }
            }
        }

        /// <summary>
        /// Stops manager
        /// </summary>
        public void Stop() {
            StopAnnounces();
            UnscheduleAnnounceUpdates();
            while(_myPool.Count > 0) {
                Dispose(_myPool.Take());
            }
        }

        /// <summary>
        /// Clears information about specified peer
        /// </summary>
        /// <param name="resource">Peer info from tracker</param>
        public void Dispose(Peers.Peer resource ) {
            _peersHave.Remove( resource.IpAddress );
        }

        /// <summary>
        /// Take peer from tracker(s)
        /// </summary>
        /// <param name="token">For canceling operation</param>
        /// <returns>Peer info</returns>
        public Peer Acquire(CancellationToken token) {
            return _myPool.Take(token);
        }

        public void Dispose() {
            _myPool.Dispose();
        }

        class AnnounceUpdateJob : IJob
        {
            private Announcer _announce;
            private AnnounceManager _manager;

            public AnnounceUpdateJob(AnnounceManager manager, Announcer announce ) {
                _manager = manager;
                _announce = announce;
            } 

            public void Execute() {
                try {
                    if(_announce.Query(out List<Peers.Peer> peers)) {
                        foreach(var peer in peers) {
                            _manager._myPool.Add(peer);
                        }
                    }
                } catch(Exception e) {
                    Debug.WriteLine( $"AnnounceManage: got exception while updating announce {e}" );
                }
            }
        }
    }
}
