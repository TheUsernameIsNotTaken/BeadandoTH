using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Xml.Serialization;
using System.IO;
using DataLibrary;

namespace Server
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        struct ClientInfo
        {
            public Socket socket;   //Socket of the client
            public string strName;  //Name by which the user logged into the chat room
        }

        //Users.
        string _userFileName = "Users.xml";
        List<User> _users;

        //Active users.
        ArrayList clientList;

        //Open port.
        Socket serverSocket;

        //Buffer
        byte[] byteData = new byte[1024];

        //Delegate
        public delegate void RegFormDelegate();
        public delegate void UploadDelegate(IPAddress address, string filename, string sender, string receiver);
        public delegate void DownloadDelegate();

        //Pooling
        private bool _uploading = false;
        private bool _downloading = false;

        public MainWindow()
        {
            clientList = new ArrayList();
            InitializeComponent();
        }

        private delegate void UpdateDelegate(string pMessage);

        private void UpdateMessage(string pMessage)
        {
            this.textBox1.Text += pMessage;
        }      

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //Load in the user data.
                _users = new List<User>();
                if (File.Exists(_userFileName))
                {
                    _users = await AsyncReadData();
                }

                //We are using TCP sockets
                //Control.CheckForIllegalCrossThreadCalls = false;
                serverSocket = new Socket(AddressFamily.InterNetwork,
                                          SocketType.Stream,
                                          ProtocolType.Tcp);

                //Assign the any IP of the machine and listen on port number 1000
                IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 1000);

                //Bind and listen on the given address
                serverSocket.Bind(ipEndPoint);
                serverSocket.Listen(4);

                //Accept the incoming clients
                serverSocket.BeginAccept(new AsyncCallback(OnAccept), null);
                //serverSocket.Accept();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Server Loaded");
            }   
        }

        private void OnAccept(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = serverSocket.EndAccept(ar);

                //Start listening for more clients
                serverSocket.BeginAccept(new AsyncCallback(OnAccept), null);

                //Once the client connects then start receiving the commands from her
                clientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None,
                    new AsyncCallback(OnReceive), clientSocket);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Server Connect");
            }
        }

        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = (Socket)ar.AsyncState;
                clientSocket.EndReceive(ar);

                //Transform the array of bytes received from the user into an
                //intelligent form of object Data
                Data msgReceived = new Data(byteData);

                //We will send this object in response the users request
                Data msgToSend = new Data();

                byte[] message;

                //If the message is to login, logout, or simple text message
                //then when send to others the type of the message remains the same
                msgToSend.cmdCommand = msgReceived.cmdCommand;
                msgToSend.strName = msgReceived.strName;
                msgToSend.strRec = msgReceived.strRec;

                switch (msgReceived.cmdCommand)
                {
                    case Command.Login:

                        //Check if the user is authorized for access
                        User connectedUser = new User(msgReceived.strName, msgReceived.strMessage);
                        if(connectedUser.SearchInList(_users) >= 0)
                        {
                            //When a user logs in to the server then we add her to our
                            //list of clients
                            msgToSend.cmdCommand = Command.Accept;
                            //message = msgToSend.ToByte();
                            //clientSocket.Send(message);

                            ClientInfo clientInfo = new ClientInfo();
                            clientInfo.socket = clientSocket;
                            clientInfo.strName = msgReceived.strName;

                            clientList.Add(clientInfo);

                            //Set the text of the message that we will broadcast to all users
                            msgToSend.strMessage = "<<<" + msgReceived.strName + " csatlakozott a szobahoz>>>";
                        }
                        else
                        {
                            //Decline the user
                            msgToSend.cmdCommand = Command.Decline;
                            msgToSend.strName = msgReceived.strName;
                            msgToSend.strMessage = null;
                            message = msgToSend.ToByte();

                            //Log it only on the server-side
                            UpdateDelegate update = new UpdateDelegate(UpdateMessage);
                            this.textBox1.Dispatcher.BeginInvoke(DispatcherPriority.Normal, update,
                                "<<<" + msgReceived.strName + " megprobalt a szobahoz csatlakozni engedely nelkul>>>" + "\r\n");

                            //Send the message and close the connection
                            clientSocket.Send(message, 0, message.Length, SocketFlags.None);
                            clientSocket.Shutdown(SocketShutdown.Both);
                            clientSocket.Close();
                        }

                        break;

                    case Command.Logout:

                        //When a user wants to log out of the server then we search for her 
                        //in the list of clients and close the corresponding connection

                        int nIndex = 0;
                        foreach (ClientInfo client in clientList)
                        {
                            if (client.socket == clientSocket && client.strName.Equals(msgReceived.strName))
                            {
                                clientList.RemoveAt(nIndex);
                                break;
                            }
                            ++nIndex;
                        }

                        //Send ack data before closing - Only for the disconnectiong client
                        //msgToSend.cmdCommand = Command.Logout;
                        //msgToSend.strName = msgReceived.strName;
                        msgToSend.strMessage = null;    //A parancs és név marad, az üzenet törlődik.
                        message = msgToSend.ToByte();
                        clientSocket.Send(message, 0, message.Length, SocketFlags.None);

                        //Kapcsolat leállítása
                        clientSocket.Shutdown(SocketShutdown.Both);
                        clientSocket.Close();

                        msgToSend.strMessage = "<<<" + msgReceived.strName + " elhagyta a szobat>>>";
                        break;

                    case Command.Message:

                        //Set the text of the message that we will broadcast to all  (if public)
                        msgToSend.strMessage = msgReceived.strName + " -> " +
                            (msgReceived.strRec.Equals(Data.PUBLIC_ID) ? "Mindenki" : msgReceived.strRec) +
                            " : " + msgReceived.strMessage;
                        break;

                    case Command.Upload:
                        //Get the correct IP
                        IPEndPoint ipendpoint = clientSocket.RemoteEndPoint as IPEndPoint;

                        //Create a delegate for upload handling
                        string path = Data.FILES_FOLDER + msgReceived.strName + "-" + msgReceived.strRec;
                        Directory.CreateDirectory(path);
                        UploadDelegate upload = new UploadDelegate(BeginUpload);
                        this.textBox1.Dispatcher.BeginInvoke(DispatcherPriority.Normal, 
                            upload, ipendpoint.Address, path + "\\" + msgReceived.strMessage, msgReceived.strName, msgReceived.strRec);

                        msgToSend.strMessage = "<<<" + msgReceived.strName + " megkezdte a '" + System.IO.Path.GetFileName(msgReceived.strMessage) + "' nevu falj feltolteset>>>";

                        break;

                    case Command.List:

                        //Send the names of all users in the chat room to the new user
                        msgToSend.cmdCommand = Command.List;
                        msgToSend.strName = msgReceived.strName;
                        msgToSend.strMessage = null;

                        //Collect the names of the user in the chat 
                        bool notFirst = false;
                        foreach (ClientInfo client in clientList)
                        {
                            //To keep things simple we use asterisk as the marker to separate the user names
                            if (notFirst)
                                msgToSend.strMessage += "*";
                            else
                                notFirst = true;
                            msgToSend.strMessage += client.strName;

                        }

                        message = msgToSend.ToByte();

                        //Send the name of the users in the chat room
                        clientSocket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                new AsyncCallback(OnSend), clientSocket);
                        break;
                }

                if ( !(msgToSend.cmdCommand == Command.List || msgToSend.cmdCommand == Command.Decline) )   //List and decline messages are not broadcasted
                {
                    message = msgToSend.ToByte();

                    foreach (ClientInfo clientInfo in clientList)
                    {
                        if (clientInfo.socket != clientSocket || msgToSend.cmdCommand != Command.Login)
                        {
                            //A publikus az broadcast, amúgy csak a 2 partnernek megy. -> Figyelni kell, hogy PUBLIC legyen alapból a cél, és feladó is legyen mindig.
                            if (msgReceived.strRec.Equals(Data.PUBLIC_ID) || clientInfo.strName.Equals(msgReceived.strRec) || clientInfo.strName.Equals(msgReceived.strName))
                            {
                                //Send the message to all users
                                clientInfo.socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                    new AsyncCallback(OnSend), clientInfo.socket);
                                //clientInfo.socket.Send(message, 0, message.Length, SocketFlags.None);
                            }
                        }
                    }
                    //textBox1.Text += msgToSend.strMessage;
                    
                    UpdateDelegate update = new UpdateDelegate(UpdateMessage);
                    this.textBox1.Dispatcher.BeginInvoke(DispatcherPriority.Normal, update, 
                        msgToSend.strMessage + "\r\n");
                }

                //If the user is logging out or declined, then we need not listen from her
                if ( !(msgReceived.cmdCommand == Command.Logout || msgToSend.cmdCommand == Command.Decline) )
                {
                    //Start listening to the message send by the user
                    clientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(OnReceive), clientSocket);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Server Receive");
            }
        }

        public void OnSend(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndSend(ar);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Server Send");
            }
        }

        //Users
        List<User> UpdateUsers()
        {
            List<User> users = new List<User>();

            XmlSerializer sr = new XmlSerializer(users.GetType());
            using (TextReader writer = new StreamReader(_userFileName))
            {
                users = (List<User>)sr.Deserialize(writer);
            }

            return users;
        }

        void SaveUsers()
        {
            XmlSerializer sr = new XmlSerializer(_users.GetType());
            using (TextWriter writer = new StreamWriter(_userFileName))
            {
                sr.Serialize(writer, _users);
            }
        }

        async Task<List<User>> AsyncReadData()
        {
            var users = await Task.Run(UpdateUsers);
            return users;
        }

        async Task AsyncSaveData()
        {
            await Task.Run(SaveUsers);
        }

        private void buttonAddUser_Click(object sender, RoutedEventArgs e)
        {
            RegFormDelegate regForm = new RegFormDelegate(RegForm);
            this.Dispatcher.Invoke(regForm);
        }

        private async void RegForm()
        {
            RegisterWindow reg_form;
            reg_form = new RegisterWindow();
            if (reg_form.ShowDialog() ?? false)
            {
                //Get new data
                User userToAdd = reg_form.NewUser;

                //Check if the username is allowed.
                if (userToAdd.username.Contains('*') || userToAdd.username.Equals(Data.PUBLIC_ID) || userToAdd.username.Contains('-'))
                {
                    new Thread(() =>
                    {
                        MessageBox.Show("Új felhasználó létrehozása sikertelen. Ok: Érvénytelen felhasználónév.", "SGSserver");
                    }).Start();
                }
                else if (_users.Where(user => user.username.Equals(userToAdd.username)).Count() > 0)
                {
                    new Thread(() =>
                    {
                        MessageBox.Show("Új felhasználó létrehozása sikertelen. Ok: Létező felhasználónév.", "SGSserver");
                    }).Start();
                }
                //Update with the authorized new user's data.
                else
                {
                    _users.Add(userToAdd);
                    await AsyncSaveData();
                }
            }
        }

        private async void BeginUpload(IPAddress address, string filename, string sender, string receiver)
        {

            //Wait for others to finish
            if (_uploading)
            {
                Thread.Sleep(2000);
            }

            //Set the pooling flag
            _uploading = true;

            try
            {
                var listener = new TcpListener(address, Data.UPLOAD_PORT);
                listener.Start();
                using (var client = await listener.AcceptTcpClientAsync())
                using (var stream = client.GetStream())
                using (var output = File.Create(filename))
                {
                    //Console.WriteLine("Client connected. Starting to receive the file.");

                    // read the file in chunks of 1KB
                    var buffer = new byte[1024];
                    int bytesRead;
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, bytesRead);
                    }
                }
                listener.Stop();
                //Console.WriteLine("Client Disconnected.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Server Upload");
            }

            _uploading = false;

            //Send an upload ack message
            byte[] message;
            Data msgToSend = new Data();

            msgToSend.cmdCommand = Command.Message;
            msgToSend.strName = sender;
            msgToSend.strRec = receiver;
            msgToSend.strMessage = sender + " -> " +
                (receiver.Equals(Data.PUBLIC_ID) ? "Mindenki" : receiver) +
                " : '" + System.IO.Path.GetFileName(filename) + "' nevu fajl sikeresen megosztva.";

            message = msgToSend.ToByte();

            try
            {
                foreach (ClientInfo clientInfo in clientList)
                {
                    //A publikus az broadcast, amúgy csak a 2 partnernek megy. -> Figyelni kell, hogy PUBLIC legyen alapból a cél, és feladó is legyen mindig.
                    if (receiver.Equals(Data.PUBLIC_ID) || clientInfo.strName.Equals(sender) || clientInfo.strName.Equals(receiver))
                    {
                        //Send the message to all users
                        clientInfo.socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                            new AsyncCallback(OnSend), clientInfo.socket);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Server Upload Message");
            }

            UpdateDelegate update = new UpdateDelegate(UpdateMessage);
            await this.textBox1.Dispatcher.BeginInvoke(DispatcherPriority.Normal, update,
                msgToSend.strMessage + "\r\n");
        }
    }
}