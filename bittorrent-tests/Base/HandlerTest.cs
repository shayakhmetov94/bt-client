using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bittorrent_tests
{
    //[TestFixture]
    //public class PeerHandlerTest
    //{
    //    public static IEnumerable<TestCaseData> MetainfoTestCaseData { get {
                
    //            yield return  new TestCaseData( Convert.FromBase64String( "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" ) );
    //        } }

    //    [Test(Description ="Creates connection between qbittorrent")]
    //    [TestCaseSource(nameof( MetainfoTestCaseData ) )]
    //    public void CreateTestScheme(byte[] meta) {
    //        Client client = new Client(new StreamingTest.Bittorrent.Metainfo(meta, Encoding.UTF8), 69998, Encoding.UTF8);

    //        Peer testPeer = new Peer( "127.0.0.1", 8999 );

    //        TcpClient connectionClient = new TcpClient();
    //        connectionClient.Connect( testPeer.IpAddress, testPeer.Port );

    //        Assert.IsTrue( connectionClient.Connected, "Peer is not connected" );
    //        PeerHandler handler = new PeerHandler(new Peer("127.0.0.1", 8999), connectionClient, client);

    //        Assert.IsTrue( handler.SendMyHandshake(), "Peer does not accepted our handshake" );
    //        Assert.IsTrue( handler.ParseAndValidateHandshake(), "Can't validate peer handshake" );

    //        handler.StartListening();
    //        handler.SendInterested( true );

    //        ManualResetEvent resetEvnt = new ManualResetEvent(false);
    //        handler.OnUnchoke += ( h ) => { resetEvnt.Reset(); };
    //        resetEvnt.WaitOne();

    //        //handler.SendChoke( false );

    //        handler.RequestPiece( new Piece( client, 0, 1024, 1024 ), 0, 1024 );
    //    }
    //}
}
