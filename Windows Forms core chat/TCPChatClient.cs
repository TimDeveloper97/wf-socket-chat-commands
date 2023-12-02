using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using Windows_Forms_CORE_CHAT_UGH;

//reference: https://github.com/AbleOpus/NetworkingSamples/blob/master/MultiClient/Program.cs
namespace Windows_Forms_Chat
{
    public class TCPChatClient : TCPChatBase
    {
        public string _name = null;
        public Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        public ClientSocket clientSocket = new ClientSocket();


        public int serverPort;
        public string serverIP;


        public static TCPChatClient CreateInstance(int port, int serverPort, string serverIP, TextBox chatTextBox)
        {
            TCPChatClient tcp = null;
            //if port values are valid and ip worth attempting to join
            if (port > 0 && port < 65535 &&
                serverPort > 0 && serverPort < 65535 &&
                serverIP.Length > 0 &&
                chatTextBox != null)
            {
                tcp = new TCPChatClient();
                tcp.port = port;
                tcp.serverPort = serverPort;
                tcp.serverIP = serverIP;
                tcp.chatTextBox = chatTextBox;
                tcp.clientSocket.socket = tcp.socket;

            }

            return tcp;
        }

        public void ConnectToServer()
        {
            int attempts = 0;

            while (!socket.Connected)
            {
                try
                {
                    attempts++;
                    SetChat("Connection attempt " + attempts);

                    // Change IPAddress.Loopback to a remote IP to connect to a remote host.
                    socket.Connect(serverIP, serverPort);
                }
                catch (SocketException)
                {
                    chatTextBox.Text = "";
                }
            }

            //Console.Clear();
            AddToChat("Connected");
            //keep open thread for receiving data
            clientSocket.socket.BeginReceive(clientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, clientSocket);
        }

        public void SendString(string text)
        {
            if(!socket.Connected)
            {
                MessageBox.Show("You aren't connect.");
                return;
            }

            var existCommand = text.Trim()[0] == '!';
            if (existCommand)
            {
                var split = text.Split(' ');
                var nameCommand = split[0].Trim().ToLower();
                if (!Common._commands.Contains(nameCommand))
                {
                    MessageBox.Show("Command not correct.");
                    return;
                }
            }

            #region handle commands
            if (text.ToLower().Contains(Common.C_USERNAME + Common.SPACE))
            {
                // username has set
                if (!string.IsNullOrEmpty(_name))
                {
                    MessageBox.Show("Command fail");
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(_name)
                && text.ToLower().Contains(Common.C_USER + Common.SPACE))
            {
                //check new username empty
                var newusername = text.Replace(Common.C_USER, "").Trim();
                if (string.IsNullOrEmpty(newusername))
                {
                    MessageBox.Show("New username can't null or empty");
                    return;
                }
                else
                {
                    byte[] buffer = Encoding.ASCII.GetBytes(text + $";{_name}");
                    socket.Send(buffer, 0, buffer.Length, SocketFlags.None);
                    return;
                }
            }
            else if (text.ToLower().Contains(Common.C_WHISPER + Common.SPACE))
            {
                //check new username empty
                var target = text.Replace(Common.C_WHISPER, "").Trim();
                if (string.IsNullOrEmpty(target))
                {
                    MessageBox.Show("Target username can't null or empty");
                    return;
                }
            }
            else if (text.ToLower() == Common.C_EXIT 
                || text.ToLower() == Common.C_COMMANDS)
            {
                //exit the chat
                byte[] buffer = Encoding.ASCII.GetBytes(text);
                socket.Send(buffer, 0, buffer.Length, SocketFlags.None);
                return;
            }

            #endregion

            //checked username exist
            if (_name == null)
            {
                if (!text.ToLower().Contains(Common.C_USERNAME + Common.SPACE))
                {
                    MessageBox.Show("You must enter name befor chat");
                    return;
                }

                //check username empty
                var username = text.Replace(Common.C_USERNAME, "").Trim();
                if (string.IsNullOrEmpty(username))
                {
                    MessageBox.Show("Username can't null or empty");
                    return;
                }
                else
                {
                    _name = username;
                    byte[] buffer = Encoding.ASCII.GetBytes(text);
                    socket.Send(buffer, 0, buffer.Length, SocketFlags.None);
                }
            }
            else
            {
                var data = _name == null ? text : $"[{_name}] " + text;
                byte[] buffer = Encoding.ASCII.GetBytes(data);
                socket.Send(buffer, 0, buffer.Length, SocketFlags.None);
            }
        }


        public void ReceiveCallback(IAsyncResult AR)
        {
            ClientSocket currentClientSocket = (ClientSocket)AR.AsyncState;

            int received;

            try
            {
                received = currentClientSocket.socket.EndReceive(AR);
            }
            catch (SocketException)
            {
                AddToChat("Client forcefully disconnected");
                // Don't shutdown because the socket may be disposed and its disconnected anyway.
                currentClientSocket.socket.Close();
                return;
            }
            //read bytes from packet
            byte[] recBuf = new byte[received];
            Array.Copy(currentClientSocket.buffer, recBuf, received);
            //convert to string so we can work with it
            string text = Encoding.ASCII.GetString(recBuf);
            Console.WriteLine("Received Text: " + text);

            //text is from server but could have been broadcast from the other clients
            if(!text.ToLower().Contains(Common.C_KICK))
                AddToChat(text);

            #region handle commands
            var newtext = text.Replace("[host]", "").Trim();
            if (text.ToLower() == Common.C_KICK)
            {
                AddToChat("Disconnected.");
                Close();
                return;
            }
            else if (!string.IsNullOrEmpty(_name)
               && text.ToLower().Contains(Common.C_USER + Common.SPACE))
            {
                //update new username empty
                _name = text.Replace(Common.C_USER, "").Trim();
            }

            #endregion

            //we just received a message from this socket, better keep an ear out with another thread for the next one
            currentClientSocket.socket.BeginReceive(currentClientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, currentClientSocket);
        }

        public void Close()
        {
            socket.Close();
        }

        public bool IsClose() => socket.Connected;
    }

}
