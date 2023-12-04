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
using System.Data.SQLite;
using System.Data;

//https://github.com/AbleOpus/NetworkingSamples/blob/master/MultiServer/Program.cs
namespace Windows_Forms_Chat
{
    public class TCPChatServer : TCPChatBase
    {
        public List<string> _names = new List<string>();
        public Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private UserRepository _userRepository = new UserRepository();
        private bool _isPlayer1 = false, _isPlayer2 = false;
        // create db
        public SQLiteConnection _connection;

        //connected clients
        public List<ClientSocket> clientSockets = new List<ClientSocket>();

        public static TCPChatServer createInstance(int port, TextBox chatTextBox, TicTacToe ticTacToe)
        {
            TCPChatServer tcp = null;

            //setup if port within range and valid chat box given
            if (port > 0 && port < 65535 && chatTextBox != null)
            {
                tcp = new TCPChatServer();
                tcp.port = port;
                tcp.chatTextBox = chatTextBox;
                tcp.ticTacToe = ticTacToe;
            }

            //return empty if user not enter useful details
            return tcp;
        }

        public void SetupDb()
        {
            string databasePath = "chat.db";

            // Connection string for SQLite
            string connectionString = $"Data Source={databasePath};Version=3;";

            _connection = new SQLiteConnection(connectionString);
            _connection.Open();

            // Create a table (if not exists)
            using (SQLiteCommand createTableCmd =
                new SQLiteCommand("CREATE TABLE IF NOT EXISTS User (Id INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT, Password TEXT, Win INTEGER, Draw INTEGER, Lose INTEGER);", _connection))
            {
                createTableCmd.ExecuteNonQuery();
            }

            // get list usersname
            _names = _userRepository.GetAll(_connection).Select(x => x.Username).ToList();
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

            // update state
            newClientSocket.state = State.Login;

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
                byte[] data = Encoding.ASCII.GetBytes(
                   (char)13 + "\n-------------------------------------"
                 + (char)13 + "\nSERVER COMMAND LIST:"
                 + (char)13 + "\n!user: Set your new user name."
                 + (char)13 + "\nCommand Syntax: !user[space]your_new_name"
                 + (char)13 + "\n-------------------------------------"
                 + (char)13 + "\n!who: Shows a list of all connected clients."
                 + (char)13 + "\n-------------------------------------"
                 + (char)13 + "\n!about: Show information about this application."
                 + (char)13 + "\n-------------------------------------"
                 + (char)13 + "\n!timestamps: Show the date and time of messages."
                 + (char)13 + "\n-------------------------------------"
                 + (char)13 + "\n!whisper: Send a private message to a person."
                 + (char)13 + "\nCommand Syntax: !whisper[space]to_client_name[space]message"
                 + (char)13 + "\n-------------------------------------"
                 + (char)13 + "\n!exit: Disconnect from the server."
                 + (char)13 + "\n-------------------------------------"
                 + (char)13 + "\n!login: Login to the server."
                 + (char)13 + "\nCommand Syntax: !login[space][username][space][password]"
                 + (char)13 + "\n-------------------------------------"
                 + (char)13 + "\n!showpassword: Show password of current user."
                 + (char)13 + "\n-------------------------------------"
                 + (char)13 + "\n!password: Change password of current user."
                 + (char)13 + "\nCommand Syntax: !password[space][newpassword]"
                 + (char)13 + "\n-------------------------------------"
                 + (char)13 + "\n!status: Get status of current user."
                 + (char)13 + "\n-------------------------------------"
                 + (char)13 + "\n!scores: Get all scores of all user."
                 + (char)13 + "\n-------------------------------------"
                 + (char)13 + "\n!join: join the game.");
                currentClientSocket.socket.Send(data);
                AddToChat("Commands sent to client");
            }
            else if (text.ToLower() == Common.C_EXIT) // Client wants to exit gracefully
            {
                currentClientSocket.socket.Send(Encoding.ASCII.GetBytes(Common.C_EXIT));
                // Always Shutdown before closing
                currentClientSocket.socket.Shutdown(SocketShutdown.Both);
                currentClientSocket.socket.Close();

                // out game
                if (currentClientSocket.name_player != null)
                {
                    if (currentClientSocket.name_player == Common.C_PLAYER1)
                        _isPlayer1 = false;
                    else if (currentClientSocket.name_player == Common.C_PLAYER2)
                        _isPlayer2 = false;
                }

                // remove names
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
                        exist.state = State.Chatting;

                        _names.Add(username);
                        SendToAll($"Username {username} has set success.", exist);
                    }

                    // insert to db
                    _userRepository.Insert(_connection, new User
                    {
                        Username = username,
                        Password = Common.DEFAULT_PASSWORD,
                    });
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
                    byte[] data = Encoding.ASCII.GetBytes("Username you type has exist.\n Disconnected.");
                    byte[] kick = Encoding.ASCII.GetBytes(Common.C_KICK);
                    currentClientSocket.socket.Send(data);
                    currentClientSocket.socket.Send(kick);
                    _names.Remove(newusername);

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
                byte[] success = Encoding.ASCII.GetBytes($"Connecting users are: \n{string.Join(", ", _names)}");
                currentClientSocket.socket.Send(success);
            }
            else if (text.ToLower().Contains(Common.C_ABOUT))
            {
                var username = text.Substring(1, text.IndexOf(']') - 1);
                // send client name
                byte[] success = Encoding.ASCII.GetBytes(
                      (char)13 + "\n-------------------------------------"
                    + (char)13 + "\nInformation of Application"
                    + (char)13 + "\nCreator: Manh Hoang Khuat."
                    + (char)13 + "\nPurposed: Assignment 2."
                    + (char)13 + "\nDevelopment Year: 2023");
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
                        tmp = $"[Whisper] Private message from [{currentClientSocket.name}] to you: " + message;
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
                if (!string.IsNullOrEmpty(currentClientSocket.name)
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
                        byte[] data = Encoding.ASCII.GetBytes(
                            $"[{username}] remove from server."
                          + (char)13 + "\n-------------------------------------------");
                        item.socket.Send(data);
                    }

                    return;
                }
                else
                {
                    byte[] success = Encoding.ASCII.GetBytes(
                            $"Username {username} doesn't exist."
                          + (char)13 + "\n-------------------------------------------");
                    currentClientSocket.socket.Send(success);
                }
            }
            else if (text.ToLower().Contains(Common.C_LOGIN + Common.SPACE))
            {
                var info = text.Replace(Common.C_LOGIN, "").Trim();
                var split = info.Split(' ');

                if (split.Length == 2)
                {
                    var username = split[0];
                    var password = split[1];

                    var users = _userRepository.GetAll(_connection);
                    var exist = users.FirstOrDefault(x => x.Username == username && x.Password == password);
                    var existLogin = clientSockets.FirstOrDefault(x => x.name == username);

                    string message;
                    if(existLogin != null)
                        message = "Account has login in another machine.";
                    else if (exist != null)
                    {
                        message = $"{Common.C_LOGIN} {username}";
                        foreach (var item in clientSockets)
                        {
                            if (item.name != currentClientSocket.name)
                                currentClientSocket.socket
                                    .Send(Encoding.ASCII.GetBytes($"Username {currentClientSocket.name} has login."));
                        }
                    }
                    else
                        message = "Username or password doesn't exist.";

                    byte[] data = Encoding.ASCII.GetBytes(message);
                    currentClientSocket.socket.Send(data);

                    // update state
                    var existClient = clientSockets.FirstOrDefault(x => x == currentClientSocket);
                    if (existClient != null)
                    {
                        existClient.state = State.Chatting;
                        existClient.name = username;

                        if(exist != null)
                        {
                            existClient.win = exist.Win;
                            existClient.draw = exist.Draw;
                            existClient.lose = exist.Lose;
                        }
                    }
                }
            }
            else if (text.ToLower() == Common.C_STATUS)
            {
                currentClientSocket.socket.Send(Encoding.ASCII.GetBytes($"Your status is {currentClientSocket.state}"));
            }
            else if (text.ToLower() == Common.C_SHOWPASSWORD)
            {
                var exist = _userRepository.GetAll(_connection).FirstOrDefault(x => x.Username == currentClientSocket.name);
                string message;
                if (exist != null)
                    message = $"Your password is {exist.Password}";
                else
                    message = $"You are not login or register.";
                currentClientSocket.socket.Send(Encoding.ASCII.GetBytes(message));
            }
            else if (text.ToLower().Contains(Common.C_PASSWORD))
            {
                var newpassword = text.Replace(Common.C_PASSWORD, "").Trim();
                _userRepository.Update(_connection, currentClientSocket.name, $"Password = '{newpassword}'");

                currentClientSocket.socket.Send(Encoding.ASCII.GetBytes("Update password success."));
            }
            else if (text.ToLower() == Common.C_JOIN)
            {
                string message;
                if (!_isPlayer1)
                {
                    currentClientSocket.name_player = Common.C_PLAYER1;
                    currentClientSocket.state = State.Playing;

                    message = $"{Common.C_PLAYER1} is User {currentClientSocket.name}";
                    _isPlayer1 = true;

                    SendToAll(message, null);
                }
                else if(!_isPlayer2)
                {
                    currentClientSocket.name_player = Common.C_PLAYER2;
                    currentClientSocket.state = State.Playing;

                    message = $"{Common.C_PLAYER2} is User {currentClientSocket.name}";
                    _isPlayer2 = true;

                    SendToAll(message, null);
                }
                else if(currentClientSocket.name_player != null)
                {
                    message = $"You are {currentClientSocket.name_player}";
                    currentClientSocket.socket.Send(Encoding.ASCII.GetBytes(message));
                }
                else
                {
                    message = $"TicTacToe game current out of slot.";
                    SendToAll(message, null);
                }
                
                // enough player
                if(_isPlayer1 && _isPlayer2)
                {
                    foreach (var client in clientSockets)
                    {
                        if(client.name_player != null)
                        {
                            client.socket.Send(Encoding.ASCII.GetBytes($"{Common.C_START} {client.name_player}"));
                            client.state = State.Playing;
                        }
                    }

                    Thread.Sleep(10);
                    SendToAll("The number of people is enough. The game begins.", null);

                    var player1 = clientSockets.FirstOrDefault(x => x.name_player == Common.C_PLAYER1);
                    SendToAll($"Now, It's {player1.name} turn.", null);

                    //reset board server
                    Action(x => x.ResetBoard());
                }
            }
            else if (text.ToLower().Contains(Common.C_POINT))
            {
                var point = text.Replace(Common.C_POINT, "").Trim();
                if (!int.TryParse(point, out var newPoint)
                    || newPoint > 8 || newPoint < 0)
                {
                    MessageBox.Show("Point must be number range [0-8]");
                    return;
                }

                var cursor = currentClientSocket.name_player == Common.C_PLAYER1 ? 'x' : 'o';
                var result = "";

                Action(x =>
                {
                    x.SetTile(newPoint, TicTacToe.StringToGridTileType(cursor));
                    result = x.GridToString();
                });

                foreach (var client in clientSockets)
                    client.socket.Send(Encoding.ASCII.GetBytes($"{Common.C_POINT} {result};{currentClientSocket.name_player}"));

                Thread.Sleep(10);
                var next = clientSockets.FirstOrDefault(x => x.name_player != null && x.name_player != currentClientSocket.name_player);
                SendToAll($"Now, It's {next.name} turn.", null);
            }
            else if (text.ToLower().Contains(Common.C_ENDGAME))
            {
                var gs = text.Replace(Common.C_ENDGAME, "").Trim();
                UpdatePoint((GameState)int.Parse(gs));
            }
            else if (text.ToLower() == Common.C_SCORES)
            {
                string message = "";
                var users = _userRepository.GetAll(_connection).OrderByDescending(x => x.Win);
                message = $"Scores are "
                        + "\n\r[User] [Win] [Draw] [Lose]";
                foreach (var user in users)
                {
                    message += (char)13 + $"\n{user.Username}    {user.Win}    {user.Draw}     {user.Lose}";
                }

                currentClientSocket.socket.Send(Encoding.ASCII.GetBytes(message));
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
                    message = $"Promote user {username} to mod."
                    + (char)13 + "\n-------------------------------------------";
                else
                    message = $"Demote user {username}."
                    + (char)13 + "\n-------------------------------------------";

                AddToChat(message);
                SendToAll(message, from);
            }
            else if (str.ToLower() == Common.C_MODS)
            {
                var mods = clientSockets.Where(x => x.isMod);
                AddToChat("Mods are: " + (mods.Count() != 0 ? String.Join(" ", mods.Select(x => x.name)) : "empty."));
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

        public void CloseDb()
        {
            // Close the database connection when the form is closing
            if (_connection.State == ConnectionState.Open)
            {
                _connection.Close();
            }
        }

        public void UpdatePoint(GameState x_o)
        {
            var player1 = clientSockets.FirstOrDefault(x => x.name_player == Common.C_PLAYER1);
            var player2 = clientSockets.FirstOrDefault(x => x.name_player == Common.C_PLAYER2);
            string message = "";
            string winner = "";

            // player 1
            if (x_o == GameState.crossWins)
            {
                _userRepository.Update(_connection, player1.name, $"Win = '{++player1.win}'");
                _userRepository.Update(_connection, player2.name, $"Lose = '{++player2.lose}'");

                message = $"{Common.C_ENDGAME} {player1.name}";
                winner = $"The winner is {player1.name}.";
            }
            // player 2
            else if (x_o == GameState.naughtWins)
            {
                _userRepository.Update(_connection, player2.name, $"Win = '{++player2.win}'");
                _userRepository.Update(_connection, player1.name, $"Lose = '{++player1.lose}'");

                message = $"{Common.C_ENDGAME} {player2.name}";
                winner = $"The winner is {player2.name}.";
            }
            // draw
            else if (x_o == GameState.draw)
            {
                _userRepository.Update(_connection, player2.name, $"Draw = '{++player2.draw}'");
                _userRepository.Update(_connection, player1.name, $"Draw = '{++player1.draw}'");

                message = $"{Common.C_ENDGAME}";
                winner = $"The match of {player1.name} and {player2.name} is a draw";
            }

            player1.socket.Send(Encoding.ASCII.GetBytes(message));
            player2.socket.Send(Encoding.ASCII.GetBytes(message));

            // clear player
            foreach (var client in clientSockets)
            {
                if(client.name_player != null)
                {
                    client.state = State.Chatting;
                    client.name_player = null;
                }    
                else
                    client.socket.Send(Encoding.ASCII.GetBytes(winner));
            }
            _isPlayer1 = false; _isPlayer2 = false;

            SendToAll("The match has ended everyone can join the match.", null);
        }
    }
}
