﻿using System;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Net;
using irc;
using System.Linq;

namespace Client
{
    public partial class Chat : Form
    {
        string server_addr;
        const int port = 7777;
        TcpClient client = null;
        string partner_username ;

        public Chat(string myPartner, string myServer_addr)
        {
            InitializeComponent();
            server_addr = myServer_addr;
            partner_username = myPartner;
            this.Text = partner_username;
        }

        public Chat() {
            InitializeComponent();
        }

        private void btn_test_Click(object sender, EventArgs e)
        {
            try
            {
                client = new TcpClient(server_addr, port);

                ircMessage msg = new ircMessage(Home.current_user.username, partner_username, tb_msg.Text, 2);

                NetworkStream stream = client.GetStream();
                stream.Write(ircMessage.ObjToBytes(msg), 0, ircMessage.ObjToBytes(msg).Length);

                lb_chat.Items.Add($"Tu: {msg.message}");

                tb_msg.Text = ""; //Ripulisce casella di scrittura del form
                stream.Close();
                client.Close();
            }
            catch (Exception ex) {
                MessageBox.Show("Chat send exception : " + ex.Message);
            }
            
        }

        public void AddMessage(string message) {
            lb_chat.Items.Add(partner_username + ": " + message);
            //MessageBox.Show("added " + message);
        }
    }
}
