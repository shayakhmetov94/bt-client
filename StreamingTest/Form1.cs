using System;
using System.Windows.Forms;
using System.IO;
using System.Net;
using bittorrent_client.Base;
using System.Text;
using bittorrent_client.Base.Peers;

namespace Bittorrent
{
    public partial class Form1 : Form
    {
        bittorrent_client.Base.BtClient bcl;

        public Form1()
        {
            InitializeComponent();
        }

        private void startBtn_Click( object sender, EventArgs e ) {
            bcl = new bittorrent_client.Base.BtClient(new Metainfo(File.ReadAllBytes( "test.torrent" ), Encoding.UTF8), new IPEndPoint(IPAddress.Any, 4444), Encoding.UTF8, new FileStream( "movie2.tmp", FileMode.Create, FileAccess.Write, FileShare.Write)/*, dhtEndPoint: new IPEndPoint(IPAddress.Parse("37.140.36.11"), 49001)*/);
            var qbhandler = bcl.ConnectionManager.ConnectTo(new Peer(IPAddress.Loopback, 8999));
            if(qbhandler == null) {
                return;
            }
            //qbhandler.SendHave(0);
            
            qbhandler.SendBitfield(bcl.PieceStorage.GetValidPieces().ToByteArray());
            //qbhandler.SendInterested(true);
            //qbhandler.SendChoke(false);
            //bcl.PeerStateCache.AwaitUnchoke(qbhandler, bcl.HandlerExchange, 30);
            //qbhandler.SendInterested(true);
            qbhandler.OnInterested += (h) => h.SendChoke(false);
            qbhandler.StartListening();

            //bcl.MainlineManager.HashTable.Owner.PingAndPutNewContact(contactEp);

            bcl.Start(false);
        }
    }
}
