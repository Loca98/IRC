﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;

namespace Client {
    public partial class ServerSearch : Form {

        Thread discoveryThread;

        delegate void SetTextCallback(string text);

        private void SetText(string text) {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (this.lbServer.InvokeRequired) {
                SetTextCallback d = new SetTextCallback(SetText);
                this.Invoke(d, new object[] { text });
            } else {
                this.lbServer.Items.Add(text);
            }
        }

        #region ServerDiscovery
        void DiscoverServers() {
            //La porta su cui il server ascolterà le richieste di discovery
            const int port = 7778;

            //Messaggio di discovery
            byte[] requestData = Encoding.ASCII.GetBytes("DISCOVER_IRCSERVER_REQUEST");
            byte[] replyDataConf = Encoding.ASCII.GetBytes("DISCOVER_IRCSERVER_ACK");

            //Prendo gli indirizzi di tutte le interfacce di questo pc
            string hostname = Dns.GetHostName();
            IPHostEntry allLocalNetworkAddresses = Dns.Resolve(hostname);

            while (true) {
                //Attraverso tutte le interfacce
                foreach (IPAddress ip in allLocalNetworkAddresses.AddressList) {

                    //Creo una socket per ogni interfaccia su cui inviare e ricevere il messaggio di discovery
                    Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                    /*
                     * Si sceglie 0 perché la porta dovrebbe essere determinata in modo automatico dal sistema
                     * non possiamo usare la porta es.7777 fissa sul client perché ci potrebbero essere altri servizi
                     * che utilizzano quella porta, sul server deve essere fissa siccome è un punto di riferimento per i
                     * client, il server invece saprà su che porta inviare i suoi messaggi ai client in base alle informazioni
                     * allegate ai messaggi
                     */
                    client.Bind(new IPEndPoint(ip, 0));

                    //Creo un'interfaccia dal quale inviare il messaggio di discovery broadcast sulla porta definita del server
                    IPEndPoint allEndPoint = new IPEndPoint(IPAddress.Broadcast, port);

                    //Abilito broadcast sul socket client
                    client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);

                    //Invio il messaggio di discovery
                    client.SendTo(requestData, allEndPoint);
                    //MessageBox.Show("Ho mandato un messaggio in broadcast", "Client notice");

                    try {
                        //Creo oggetto per il server
                        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                        EndPoint tempRemoteEP = (EndPoint)sender;
                        byte[] buffer = new byte[replyDataConf.Length];

                        //Ricevo dal server, non aspetto più di 3 secondi
                        client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 3000);

                        //Ricevo il messaggio, l'interfaccia remota provverà le informazioni necessarie come indirizzo IP e porta
                        client.ReceiveFrom(buffer, ref tempRemoteEP);
                        string response = Encoding.ASCII.GetString(buffer);
                        //MessageBox.Show($"Received {response} from {tempRemoteEP.ToString()}", "Notice");

                        if (response.Equals(Encoding.ASCII.GetString(replyDataConf))) {
                            //Qui bisognerebbe filtrare l'indirizzo IP dall'interfaccia remota
                            string serverIP = tempRemoteEP.ToString().Split(':').ToArray()[0];
                            SetText(serverIP);
                        }

                    } catch (Exception e) {
                       // MessageBox.Show(e.Message);
                    }
                }
            }
        }
#endregion

        public ServerSearch() {
            InitializeComponent();
            
            discoveryThread = new Thread(new ThreadStart(DiscoverServers));

            discoveryThread.Start();
        }

        private void BtnSubmit_Click(object sender, EventArgs e) {

            string selectedServerIp = lbServer.GetItemText(lbServer.SelectedItem);

            if (string.IsNullOrEmpty(selectedServerIp)) {
                MessageBox.Show("Prima di continuare devi scegliere un server disponibile","Avviso");
            } else {
                discoveryThread.Abort();
                new Home(/*selectedServerIp*/).Show();
            }
        }

        private void BtnReset_Click(object sender, EventArgs e) {
            discoveryThread.Abort();
            this.Close();
        }
    }
}