﻿using System;
using System.Net.Sockets;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using irc;
using System.Text;

namespace Server
{
    class Server
    {
        //private UdpClient serverListener;
        private static byte[] listenerResponseData = Encoding.ASCII.GetBytes("ACK");
        private static string listenerRequestCheck = "DISCOVER_IRCSERVER_REQUEST";
        const int port = 7777;
        Socket server = new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);
        

        public Server()
        {
            //int port = 7777;
            server.Bind(new IPEndPoint(IPAddress.Any,port));
            IPAddress ip = IPAddress.Parse("127.0.0.1");
            Console.WriteLine("Server listening...");
            /*TcpListener server = new TcpListener (ip, port);
            TcpClient client = default(TcpClient);*/

            while (true) {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any,0);
                EndPoint tempRemoteEP = (EndPoint)sender;
                byte[] buffer = new byte[Encoding.ASCII.GetByteCount(listenerRequestCheck)];
                server.ReceiveFrom(buffer,ref tempRemoteEP);
                Console.WriteLine($"Server got {Encoding.ASCII.GetString(buffer)} from {tempRemoteEP.ToString()}");

                Console.WriteLine($"Sending {Encoding.ASCII.GetString(listenerResponseData)} to {tempRemoteEP.ToString()}");

                server.SendTo(listenerResponseData, tempRemoteEP);
            }

            //serverListener = new UdpClient(port);
            

            /*try
            {
                server.Start();
                Console.WriteLine("Server started...");
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
                Console.Read();
            }

            while (true)
            {
                DiscoveryListener();

                client = server.AcceptTcpClient();

                byte[] buffer = new byte[1024];
                NetworkStream stream = client.GetStream();

                stream.Read(buffer, 0, buffer.Length);

                Console.WriteLine(BytesToObj(buffer).message);
            }*/
        }


        private void DiscoveryListener() {
            /*
            //Passando 0 usiamo qualsiasi porta scelta dal client
            IPEndPoint clientEndPoint = new IPEndPoint(IPAddress.Any,0);

            //Riceviamo i dati dal client e trasformiamo in stringa
            byte[] clientRequestData = serverListener.Receive(ref clientEndPoint);
            string clientRequestMessage = Encoding.ASCII.GetString(clientRequestData);

            Console.WriteLine($"Received {clientRequestMessage} from {clientEndPoint.Address.ToString()}");

            //Se la stringa di richiesta è giusta rispondiamo con la stringa di risposta
            if (clientRequestMessage.Equals(listenerRequestCheck)) {
                serverListener.Send(listenerResponseData, listenerResponseData.Length, clientEndPoint);
            }*/

        }

        /// <summary>
        ///  Converte un Oggetto qualsiasi in un array di byte
        /// </summary>
        /// <param msg="obj Message da convertire">
        /// </param>
        private byte[] ObjToBytes(ircMessage msg)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, msg);
                return ms.ToArray();
            }
        }

        /// <summary>
        ///  Converte un array di byte 
        /// </summary>
        /// <param msg="array di byte da convertire">
        /// </param>
        private ircMessage BytesToObj(byte[] msg)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bf = new BinaryFormatter();
                ms.Write(msg, 0, msg.Length);
                ms.Seek(0, SeekOrigin.Begin);
                return (ircMessage)bf.Deserialize(ms);
            }
        }
    }

    
}
