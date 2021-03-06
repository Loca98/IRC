﻿using System;
using System.Net.Sockets;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Data;
using System.Web.Helpers;
using System.Threading;
using System.Linq;
using irc;
using System.Net.NetworkInformation;

namespace Server
{
    class Server
    {

        //Porta su cui il server gestirà le comunicazioni discovery
        private const int discoveryPort = 7778;

        private const int port = 7777;

        private const int pingPort = 7779;

        //Lista di utenti online sul server
        List<ircUser> onlineUsers;
        Thread tcpListnerThread = null;
        string serverName;

        public Server()
        {
            //Thread per rispondere alle richieste di discovery dei Client
            Thread discoveryListener = new Thread(new ThreadStart(DiscoveryListener));

            //Thread per verificare che i Client siano ancora connessi
            Thread pingOnlineUsers = new Thread(new ThreadStart(PingUsers));

            IPAddress ip = IPAddress.Any;

            //Listener in ascolto per connessioni TCP dai client per inviare messaggi
            TcpListener server = new TcpListener(ip, port);

            //Listener in ascolto per connessioni TCP per il ping da parte dei client per verificare che il server sia attivo
            TcpListener pingListener = new TcpListener(ip, pingPort);

            onlineUsers = new List<ircUser>();
            serverName = string.Empty;

            try
            {

                server.Start();
                pingListener.Start();

                //Richiedo il nome del server da presentare al client
                do {
                    Console.Write("Inserisci un nome per il tuo server: ");
                    serverName = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(serverName)) {
                        Console.WriteLine("Nome server non valido");
                    }
                } while (string.IsNullOrWhiteSpace(serverName));
                Console.WriteLine("Server started...");
                
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
                Console.Read();
            }
            discoveryListener.Start();
            pingOnlineUsers.Start();

            tcpListnerThread = new Thread(() => {

                TcpClient client = default(TcpClient);

                while (true) {

                    //Ricevo connessioni TCP dai Client
                    client = server.AcceptTcpClient();

                    byte[] buffer = new byte[1024];
                    NetworkStream stream = client.GetStream();

                    //Leggo i messaggi inviati sullo stream
                    int len = stream.Read(buffer, 0, buffer.Length);
                    
                    ircMessage msg = (ircMessage)ircMessage.BytesToObj(buffer, len);
                    string senderAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

                    switch (msg.action) {
                        case 0: //Registrazione nuovo utente
                            Console.WriteLine("REGISTER_USER_REQUEST Received");
                            //msg.message contiene username+password; vado a dividere la stringa in 2
                            if (Register(msg.message.Split(':')[0], msg.message.Split(':')[1]) != 0)
                            {
                                Send(senderAddress, Login(msg.message.Split(':')[0], msg.message.Split(':')[1], senderAddress));
                            }
                            else
                            {
                                Console.WriteLine("Registrazione fallita");
                                Send(senderAddress, onlineUsers);
                            }
                            UpdateOnlineUsers();
                            break;
                        case 1: //Login
                            Console.WriteLine("LOGIN_USER_REQUEST Received");
                            Send(senderAddress, Login(msg.message.Split(':')[0], msg.message.Split(':')[1], senderAddress));
                            UpdateOnlineUsers();
                            break;
                        case 2: //Message
                            Console.WriteLine("MESSAGE received from " + ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString());
                            RedirectData(msg);
                            break;
                        case 3: //Logout
                            Console.WriteLine("LOGOUT_REQUEST received");
                            Logout(msg.sender_username);
                            break;
                    }

                    client.Close();
                    stream.Close();
                }
            });
            tcpListnerThread.Start();
        }

        #region discoveryListener
        /// <summary>
        ///     Server discovery listener usato per rispondere alle richieste UDP broadcast dei client.
        /// </summary>
        private void DiscoveryListener() {

            //Messaggio di risposta alla server discovery request
            byte[] listenerResponseData = Encoding.ASCII.GetBytes($"DISCOVER_IRCSERVER_ACK:{serverName}");

            //Il messaggio di richiesta dovrà corrispondere a questa stringa
            byte[] listenerRequestCheck = Encoding.ASCII.GetBytes("DISCOVER_IRCSERVER_REQUEST");

            //Creo nuovo socket UDP su cui ascoltare le richieste dei client
            Socket serverListener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            //Usando IPAddress.any indico alla socket che deve ascoltare per attività su tutte le interfacce di rete
            //Usando port indico alla socket che deve ascoltare su quella specifica porta
            serverListener.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));

            while (true) {
                /* Il sender è colui che invia una richiesta discovery
                 * in questo caso sarà un client che può avere un qualsiasi
                 * indirizzo e usare una qualsiasi porta
                 */
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                EndPoint tempRemoteEP = (EndPoint)sender;

                //Il messaggio in byte ricevuto dal client
                byte[] buffer = new byte[listenerRequestCheck.Length];

                //Riceve messaggi da chiunque
                serverListener.ReceiveFrom(buffer, ref tempRemoteEP);

                Console.WriteLine($"Server got {Encoding.ASCII.GetString(buffer)} from {tempRemoteEP.ToString()}");

                //Controllo validità del messaggio di richiesta
                if (Encoding.ASCII.GetString(buffer).Equals(Encoding.ASCII.GetString(listenerRequestCheck))) {
                    Console.WriteLine($"Sending {Encoding.ASCII.GetString(listenerResponseData)} to {tempRemoteEP.ToString()}");

                    //Invio risposta al client
                    serverListener.SendTo(listenerResponseData, tempRemoteEP);
                } else {
                    Console.WriteLine($"Bad message from {tempRemoteEP.ToString()} not replying...");
                }
            }
        }
        #endregion

        /// <summary>
        /// Per ogni utente all'interno della lista di utenti online cerca di creare una connessione TCP per verificare che siano ancora online
        /// </summary>
        void PingUsers() {
            while (true) {

                if (onlineUsers != null) {
                    foreach (ircUser user in onlineUsers.ToList()) {
                        Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")}:Invio ping TCP a {user.username} all'indirizzo {user.address}");
                        TcpClient tcpClient = new TcpClient();

                        //Provo ad avviare una connessione al client, il tempo di scadenza della connessione TCP non è modificabile
                        try {
                            tcpClient.Connect(user.address, port);
                            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")}:Connessione TCP avvenuta con successo per {user.username}");
                            tcpClient.Close();
                        } catch (Exception) {
                            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")}:Connessione TCP fallita per {user.username}");
                            Logout(user.username);
                        }

                        //Elimino la connessione
                        try {
                            tcpClient.Dispose();
                        } catch (Exception ex) {
                            Console.WriteLine(ex.Message);
                        }
                        Thread.Sleep(1000);
                    }
                }
            }
        }

        #region userAuth

        /// <summary>
        /// Autentica l'utente con le credenziali fornite
        /// </summary>
        /// <param name="username">Nome utente <see cref="string"/></param>
        /// <param name="password">Password dell'utente <see cref="string"/></param>
        /// <param name="address">Indirizzo IP da cui viene effettuata la richiesta <see cref="string"/></param>
        /// <returns><see cref="List{ircUser}"/> Lista di utenti</returns>
        private List<ircUser> Login(string username, string password, string address) {

            Console.WriteLine($"Inizio processo di login per {username}:{password}");

            //Ricerco se l'utente è già presente nella lista di utenti online su questo server
            ircUser loggedUser = onlineUsers.Where(user => user.username.Equals(username)).FirstOrDefault();

            if (loggedUser == null) {
                DBManager dbManager = new DBManager();

                DataTable result = dbManager.Query(TableNames.usersTable, $"SELECT * FROM {TableNames.usersTable} WHERE username = '{username}'");

                if (result.Rows.Count != 1) {
                    Console.WriteLine($"Login fallito per {username}");
                    return new List<ircUser>();
                } else {
                    if (Crypto.VerifyHashedPassword(result.Rows[0]["password"].ToString(), password)) {
                        Console.WriteLine("Login avvenuto con successo per " + username);
                        onlineUsers.Add(new ircUser(username, address));
                        return onlineUsers;
                    } else {
                        Console.WriteLine($"Login fallito per {username}");
                        return new List<ircUser>();
                    }
                }
            }
            Console.WriteLine($"Login fallito per {username}");
            return new List<ircUser>();
        }

        /// <summary>
        /// Registra un nuovo account sul DB con le credenziali fornite
        /// </summary>
        /// <param name="username"><see cref="string"/> nome utente</param>
        /// <param name="password"><see cref="string"/> password</param>
        /// <returns><see cref="int"/>0 fallito, altrimenti successo</returns>
        /// <remarks>Il controllo della ripetizione della password bisogna farla dalla parte richiamante della funzione</remarks>
        private int Register(string username, string password) {
            Console.WriteLine($"Inizio processo di registrazione per {username}");
            DBManager dbManager = new DBManager();

            return dbManager.Insert(TableNames.usersTable, new Dictionary<string, string> {
                {"username", username},
                {"password", Crypto.HashPassword(password)}
            });
        }

        /// <summary>
        /// Rimuove un utente dalla lista di utenti online
        /// </summary>
        /// <param name="username"><see cref="string"/>nome utene da rimuovere da trovare all'interno della lista </param>
        private void Logout(string username) {
            Console.WriteLine($"Inizio il processo di LOGOUT per {username}");
            //Cerco se l'utente è presente nella lista di utenti online su questo server
            ircUser loggedUser = onlineUsers.Where(user => user.username.Equals(username)).FirstOrDefault();

            if (loggedUser != null) {
                onlineUsers.Remove(loggedUser);
                UpdateOnlineUsers();
                Console.WriteLine($"Utente {username} trovato ed eliminato con successo dalla lista di utenti online");
            } else {
                Console.WriteLine($"Utente {username} non trovato");
            }
        }

        #endregion

        /// <summary>
        ///  Redirigo un messaggio ircMessage ad un utente
        /// </summary>
        /// <param name="msg">Messaggio ircMessage da reinderizzare</param>
        void RedirectData(ircMessage msg) {
            ircUser receiver = onlineUsers.Where(user => user.username.Equals(msg.receiver_username)).FirstOrDefault();
            if (receiver != null) {
                try
                {
                    TcpClient client = new TcpClient(receiver.address, port);

                    NetworkStream stream = client.GetStream();
                    stream.Write(ircMessage.ObjToBytes(msg), 0, ircMessage.ObjToBytes(msg).Length);

                    stream.Close();
                    client.Close();

                }
                catch (Exception ex)
                {
                    Console.WriteLine("RedirectData Exception : " + ex.Message);
                }
            } else
            {
                CacheMessage(msg);
            }
        }

        /// <summary>
        ///  Invia un messaggio 
        /// </summary>
        /// <param name="destAddres">Indirizzo di destinazione</param>
        /// <param name="users">Messaggio di tipo <see cref="irc"/> da inviare</param>
        /// <remarks>
        ///     Nel caso l'utente associato all'indirizzo non fosse trovato nella lista di utenti online verrà chiamato il metodo
        ///     <see cref="CacheMessage(ircMessage)"/>
        /// </remarks>
        void Send(string destAddress, List<ircUser> users) {
            try
            {
                TcpClient sender = new TcpClient(destAddress, port);
                byte[] data = ircMessage.ObjToBytes(users);

                NetworkStream stream = sender.GetStream();

                stream.Write(data, 0, data.Length);

                stream.Close();
                sender.Close();
            }
            catch (Exception ex) {
                Console.WriteLine("Send Exception : " + ex.Message);
            }
        }

        /// <summary>
        ///  Salva un messaggio sul database.
        /// </summary>
        /// <param name="message">
        ///     Messaggio da archiviare di tipo <see cref="ircMessage"/>
        /// </param>
        void CacheMessage(ircMessage message) {
            DBManager dbManager = new DBManager();

            Console.WriteLine($"Svolgo i controlli necessari per archiviare il messaggio per {message.receiver_username}");

            //Se per qualche motivo l'utente che ha inviato il messaggio non esiste all'interno del DB non invio il messaggio
            DataTable senderIdResult = dbManager.Query(table: TableNames.usersTable, queryString: $"SELECT user_id FROM {TableNames.usersTable} WHERE username = '{message.sender_username}';");
            if (senderIdResult.Rows.Count != 1) {
                Console.WriteLine($"Errore nel trovare mittente {message.sender_username}");
                return;
            }
            int senderId = (int)senderIdResult.Rows[0]["user_id"];

            //Se per qualche motivo l'utente che deve ricevere il messaggio non esiste nel DB non invio il messaggio
            DataTable receiverIdResult = dbManager.Query(table: TableNames.usersTable, queryString: $"SELECT user_id FROM {TableNames.usersTable} WHERE username = '{message.receiver_username}';");
            if (receiverIdResult.Rows.Count != 1) {
                Console.WriteLine($"Errore nel trovare destinatario {message.receiver_username}");
                return;
            }
            int receiverId = (int)receiverIdResult.Rows[0]["user_id"];

            Console.WriteLine($"Aggiungo ai messaggi archiviati");
            dbManager.Insert(table: TableNames.messagesTable, new Dictionary<string, string> {
                {"sender", senderId.ToString()},
                {"receiver", receiverId.ToString() },
                {"text", message.message},
                {"created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
            });
        }

        /// <summary>
        ///  Ricerca tutti i messaggi archiviati dell'utente.
        /// </summary>
        /// <param name="username">Username verso cui sono indirizzati i messaggi</param>
        /// <returns>Lista di messagi <see cref="ircMessage"/></returns>
        /// <remarks>Nel caso non ci fossero messaggi la lista sarà vuota</remarks>
        List<ircMessage> GetCachedMessages(string username) {
            List<ircMessage> messages = new List<ircMessage>();
            DBManager dbManager = new DBManager();

            //Ottengo tutti i messaggi indirizzati verso username
            DataTable messagesResult = dbManager.Query(table: TableNames.messagesTable,
                                                       queryString: $"SELECT sender, receiver text " +
                                                                    $"FROM {TableNames.messagesTable},{TableNames.usersTable} " +
                                                                    $"WHERE username = '{username}' AND receiver = user_id AND {TableNames.messagesTable}.read = 0;");

            //Uso messaggio come variabile d'appoggio per l'aggiunta alla lista di messaggi invece di fare messages.Add(new ircMessage(etc...))
            ircMessage messaggio = new ircMessage();
            //Il ricevente sarà sempre l'utente cercato
            messaggio.receiver_username = username;
            //L'azione sarà di tipo "messaggio"
            messaggio.action = 3;
            DataTable senderUsernameResult;

            //Ciclo ogni riga del risultato dei messaggi archiviati
            foreach (DataRow row in messagesResult.Rows) {

                //Ottengo il nome utente di colui che ha inviato il messaggio
                senderUsernameResult = dbManager.Query(table: TableNames.usersTable,
                                                                 queryString: $"SELECT username FROM {TableNames.usersTable} WHERE user_id = '{row["sender"].ToString()}';");

                //Controllo che quell'utente esista all'interno del DB
                if (senderUsernameResult.Rows.Count != 1) {
                    messaggio.sender_username = "Unknown user";
                } else {
                    messaggio.sender_username = senderUsernameResult.Rows[0]["username"].ToString();
                }

                messaggio.message = row["text"].ToString();

                messages.Add(messaggio);
            }

            return messages;
        }

        /// <summary>
        ///  Rimanda la lista di utenti online aggiornata a tutti i client connessi
        /// </summary>
        /// <param name="excluded_user">Username verso cui sono indirizzati i messaggi</param>
        void UpdateOnlineUsers() {
            foreach (ircUser user in onlineUsers) {
                Send(user.address, onlineUsers);
            }
        }

    }
}