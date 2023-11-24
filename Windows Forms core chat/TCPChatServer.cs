using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using Windows_Forms_CORE_CHAT_UGH;
using System.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using System.Collections;
using static System.Net.Mime.MediaTypeNames;

//https://github.com/AbleOpus/NetworkingSamples/blob/master/MultiServer/Program.cs
namespace Windows_Forms_Chat
{
    public class TCPChatServer : TCPChatBase
    {
        public List<string> _names = new List<string>();
        public Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        //connected clients
        public List<ClientSocket> clientSockets = new List<ClientSocket>();

        public static TCPChatServer createInstance(int port, TextBox chatTextBox)
        {
            TCPChatServer tcp = null;

            //setup if port within range and valid chat box given
            if (port > 0 && port < 65535 && chatTextBox != null)
            {
                tcp = new TCPChatServer();
                tcp.port = port;
                tcp.chatTextBox = chatTextBox;
            }

            //return empty if user not enter useful details
            return tcp;
        }

        public void SetupServer()
        {
            chatTextBox.Text += "Setting up server...\n";
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            serverSocket.Listen(0);

            //kick off thread to read connecting clients, when one connects, it'll call out AcceptCallback function
            serverSocket.BeginAccept(AcceptCallback, this);
            chatTextBox.Text += "Server setup complete\n";
        }

        public void CloseAllSockets()
        {
            foreach (ClientSocket clientSocket in clientSockets)
            {
                clientSocket.socket.Shutdown(SocketShutdown.Both);
                clientSocket.socket.Close();
            }

            clientSockets.Clear();
            serverSocket.Close();
        }

        public void AcceptCallback(IAsyncResult AR)
        {
            Socket joiningSocket;

            try
            {
                joiningSocket = serverSocket.EndAccept(AR);
            }
            catch (ObjectDisposedException) // I cannot seem to avoid this (on exit when properly closing sockets)
            {
                return;
            }

            ClientSocket newClientSocket = new ClientSocket();
            newClientSocket.socket = joiningSocket;

            clientSockets.Add(newClientSocket);
            //start a thread to listen out for this new joining socket. Therefore there is a thread open for each client
            joiningSocket.BeginReceive(newClientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, newClientSocket);
            AddToChat("Client connected, waiting for request...");

            //we finished this accept thread, better kick off another so more people can join
            serverSocket.BeginAccept(AcceptCallback, null);
        }

        public void ReceiveCallback(IAsyncResult AR)
        {
            ClientSocket currentClientSocket = (ClientSocket)AR.AsyncState;

            int received = 0;

            try
            {
                if (currentClientSocket.socket.Connected)
                    received = currentClientSocket.socket.EndReceive(AR);
            }
            catch (SocketException)
            {
                AddToChat("Client forcefully disconnected");

                // Don't shutdown because the socket may be disposed and its disconnected anyway.
                currentClientSocket.socket.Close();
                clientSockets.Remove(currentClientSocket);
                return;
            }

            byte[] recBuf = new byte[received];
            Array.Copy(currentClientSocket.buffer, recBuf, received);
            string text = Encoding.ASCII.GetString(recBuf);

            if (!text.ToLower().Contains(Common.C_USER + Common.SPACE))
                AddToChat(currentClientSocket.isMod ? "[mod]" + text : text);

            #region handle commands
            if (text.ToLower() == Common.C_COMMANDS) // Client requested time
            {
                var commands = Common._commands;
                commands.Remove(Common.C_KICK);
                byte[] data = Encoding.ASCII.GetBytes("Commands are " + String.Join(" ", commands));
                currentClientSocket.socket.Send(data);
                AddToChat("Commands sent to client");
            }
            else if (text.ToLower() == Common.C_EXIT) // Client wants to exit gracefully
            {
                // Always Shutdown before closing
                currentClientSocket.socket.Shutdown(SocketShutdown.Both);
                currentClientSocket.socket.Close();

                if (!string.IsNullOrEmpty(currentClientSocket.name))
                    _names.Remove(currentClientSocket.name);

                clientSockets.Remove(currentClientSocket);
                AddToChat("Client disconnected");
                return;
            }
            else if (text.ToLower().Contains(Common.C_USERNAME))
            {
                var username = text.Replace(Common.C_USERNAME, "").Trim();

                //user exist close socket 
                if (_names.Contains(username))
                {
                    byte[] data = Encoding.ASCII.GetBytes("Username you type has exist.\nDisconnected");
                    byte[] kick = Encoding.ASCII.GetBytes(Common.C_KICK);
                    currentClientSocket.socket.Send(data);
                    currentClientSocket.socket.Send(kick);

                    AddToChat("Client disconnected");

                    // Don't shutdown because the socket may be disposed and its disconnected anyway.
                    currentClientSocket.socket.Close();
                    clientSockets.Remove(currentClientSocket);
                    return;
                }
                else
                {
                    var exist = clientSockets.FirstOrDefault(x => x == currentClientSocket);
                    if (exist != null)
                    {
                        exist.name = username;

                        _names.Add(username);
                        SendToAll($"Username {username} has set success.", exist);
                    }
                }
            }
            else if (text.ToLower().Contains(Common.C_USER))
            {
                var names = text.Replace(Common.C_USER, "").Trim().Split(';');
                var newusername = names[0];
                var username = names[1];

                //user exist close socket 
                if (_names.Contains(newusername))
                {
                    byte[] data = Encoding.ASCII.GetBytes("Username you type has exist.\nDisconnected");
                    byte[] kick = Encoding.ASCII.GetBytes(Common.C_KICK);
                    currentClientSocket.socket.Send(data);
                    currentClientSocket.socket.Send(kick);

                    AddToChat("Client forcefully disconnected");

                    // Don't shutdown because the socket may be disposed and its disconnected anyway.
                    currentClientSocket.socket.Close();
                    clientSockets.Remove(currentClientSocket);
                    return;
                }
                else
                {
                    _names.Add(newusername);
                    _names.Remove(username);
                    byte[] success = Encoding.ASCII.GetBytes(Common.C_USER + " " + newusername);
                    currentClientSocket.socket.Send(success);

                    //normal message broadcast out to all clients
                    HandleSendToAll($"User [{username}] has change name to [{newusername}]", currentClientSocket);
                }
            }
            else if (text.ToLower().Contains(Common.C_WHO))
            {
                //var username = text.Substring(1, text.IndexOf(']') - 1);
                // send client name
                byte[] success = Encoding.ASCII.GetBytes($"Users are {string.Join(" ", _names)}");
                currentClientSocket.socket.Send(success);
            }
            else if (text.ToLower().Contains(Common.C_ABOUT))
            {
                var username = text.Substring(1, text.IndexOf(']') - 1);
                // send client name
                byte[] success = Encoding.ASCII.GetBytes($"Project develop by Khuat and Hai.\nTarget using for work and study.\nTime: {DateTime.Now}");
                currentClientSocket.socket.Send(success);
            }
            else if (text.ToLower().Contains(Common.C_WHISPER))
            {
                var username = text.Substring(1, text.IndexOf(']') - 1);

                //check new username empty
                var newText = text.Substring(text.IndexOf(Common.C_WHISPER) + Common.C_WHISPER.Length).Trim();
                var split = newText.Split(' ');
                var target = split[0];
                var message = split.Length < 2 ? "" : String.Join(" ", split.Skip(1));

                if (!_names.Contains(target.ToLower().Trim()))
                {
                    byte[] success = Encoding.ASCII.GetBytes($"Username not exist.");
                    currentClientSocket.socket.Send(success);
                }
                else
                {
                    var socket = clientSockets.FirstOrDefault(x => x.name == target);
                    var tmp = "";
                    if (socket.name != currentClientSocket.name)
                        tmp = $"Private from [{currentClientSocket.name}] to [{socket.name}] " + message;
                    else
                        tmp = $"You can't send message to you.";

                    byte[] success = Encoding.ASCII.GetBytes(tmp);
                    socket.socket.Send(success);
                }
            }
            else if (text.ToLower().Contains(Common.C_TIMESTAMPS))
            {
                //exit the chat
                var exist = clientSockets.FirstOrDefault(x => x.socket == currentClientSocket.socket);
                exist.isTime = !exist.isTime;
                var message = exist.isTime ? "Enable time success." : "Disable time success.";
                byte[] buffer = Encoding.ASCII.GetBytes(message);
                exist.socket.Send(buffer, 0, buffer.Length, SocketFlags.None);
            }
            else if (text.ToLower().Contains(Common.C_KICK + Common.SPACE))
            {
                var newText = text.Substring(text.IndexOf(Common.C_KICK));
                var username = newText.Replace(Common.C_KICK, "").Trim();

                // user exist close socket 
                if(!string.IsNullOrEmpty(currentClientSocket.name) 
                    && username == currentClientSocket.name)
                {
                    byte[] success = Encoding.ASCII.GetBytes($"You can't kick you.");
                    currentClientSocket.socket.Send(success);
                }
                else if (_names.Contains(username))
                {
                    var exist = clientSockets.FirstOrDefault(x => x.name == username);
                    byte[] kick = Encoding.ASCII.GetBytes(Common.C_KICK);
                    exist.socket.Send(kick);

                    AddToChat("Client disconnected");

                    // Don't shutdown because the socket may be disposed and its disconnected anyway.
                    exist.socket.Close();
                    clientSockets.Remove(exist);
                    _names.Remove(username);
                    foreach (var item in clientSockets)
                    {
                        byte[] data = Encoding.ASCII.GetBytes($"[{username}] remove from server.");
                        item.socket.Send(data);
                    }

                    return;
                }
                else
                {
                    byte[] success = Encoding.ASCII.GetBytes($"Username {username} doesn't exist.");
                    currentClientSocket.socket.Send(success);
                }
            }
            else
            {
                //normal message broadcast out to all clients
                HandleSendToAll(text, currentClientSocket);
            }
            #endregion

            //we just received a message from this socket, better keep an ear out with another thread for the next one
            if (currentClientSocket.socket != null && currentClientSocket.socket.Connected)
                currentClientSocket.socket.BeginReceive(currentClientSocket.buffer, 0, ClientSocket.BUFFER_SIZE, SocketFlags.None, ReceiveCallback, currentClientSocket);
        }

        public void HandleSendToAll(string str, ClientSocket from)
        {
            if (str.ToLower().Contains(Common.C_MOD + Common.SPACE))
            {
                var username = str.Replace(Common.C_MOD, "").Trim();
                if (!_names.Contains(username))
                {
                    MessageBox.Show("Username not exist.");
                    return;
                }

                var exist = clientSockets.FirstOrDefault(x => x.name == username);
                exist.isMod = !exist.isMod;
                var message = "";
                if (exist.isMod)
                    message = $"Promote user {username} to mod.";
                else
                    message = $"Demote user {username}.";

                AddToChat(message);
                SendToAll(message, from);
            }
            else if (str.ToLower() == Common.C_MODS)
            {
                var mods = clientSockets.Where(x => x.isMod);
                AddToChat("Mods are " + (mods.Count() != 0 ? String.Join(" ", mods.Select(x => x.name)) : "empty."));
            }
            else if (str.ToLower().Contains(Common.C_KICK + Common.SPACE))
            {
                var username = str.Replace(Common.C_KICK, "").Trim();
                if (!_names.Contains(username))
                {
                    MessageBox.Show("Username not exist.");
                    return;
                }

                _names.Remove(username);
                var exist = clientSockets.FirstOrDefault(x => x.name == username);
                byte[] kick = Encoding.ASCII.GetBytes(Common.C_KICK);
                exist.socket.Send(kick);

                AddToChat("Client forcefully disconnected");

                // Don't shutdown because the socket may be disposed and its disconnected anyway.
                exist.socket.Close();
                clientSockets.Remove(exist);

                foreach (var item in clientSockets)
                {
                    byte[] data = Encoding.ASCII.GetBytes($"[{username}] remove from server.");
                    item.socket.Send(data);
                }
                return;
            }
            else
                SendToAll(str, from);
        }

        public void SendToAll(string str, ClientSocket from)
        {
            var host = from == null ? "[host] " : from.isMod ? "[mod]" : "";
            foreach (ClientSocket c in clientSockets)
            {
                var time = c.isTime ? $"[{DateTime.Now.ToString("dd/MM/yyyy HH:mm")}]" : "";
                if (from == null || !from.socket.Equals(c))
                {
                    byte[] data = Encoding.ASCII.GetBytes(time + host + str);
                    if (c.socket.Connected)
                        c.socket.Send(data);
                }
            }
        }
    }
}
