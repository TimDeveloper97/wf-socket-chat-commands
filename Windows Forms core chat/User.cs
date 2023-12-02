using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Windows_Forms_CORE_CHAT_UGH
{
    using System.Data.SQLite;

    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int Win { get; set; } = 0;
        public int Draw { get; set; } = 0;
        public int Lose { get; set; } = 0;
    }


    public class UserRepository
    {
        public IEnumerable<User> GetAll(SQLiteConnection connection)
        {
            var l = new List<User>();
            // Query data
            using (SQLiteCommand selectCmd = new SQLiteCommand("SELECT * FROM User;", connection))
            {
                using (SQLiteDataReader reader = selectCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // Access data using reader["ColumnName"]
                        Console.WriteLine($"Id: {reader["Id"]}, Username: {reader["Username"]}, Win: {reader["Win"]}");
                        
                        var u = new User
                        {
                            Id = int.Parse(reader["Id"].ToString()),
                            Username = reader["Username"].ToString(),
                            Password = reader["Password"].ToString(),
                            Win = int.Parse(reader["Win"].ToString()),
                            Draw = int.Parse(reader["Draw"].ToString()),
                            Lose = int.Parse(reader["Lose"].ToString()),
                        };

                        l.Add(u);
                    }
                    
                }
            }

            return l;
        }

        public void Insert(SQLiteConnection connection, User u)
        {
            using (SQLiteCommand insertCmd = 
                new SQLiteCommand($"INSERT INTO User (Username, Password, Win, Draw, Lose) VALUES ('{u.Username}', '{u.Password}', {u.Win}, {u.Draw}, {u.Lose});", connection))
            {
                insertCmd.ExecuteNonQuery();
            }
        }

        public void Update(SQLiteConnection connection, int id, string where)
        {
            using (SQLiteCommand updateCmd = 
                new SQLiteCommand($"UPDATE User SET Id = {id} WHERE {where};", connection))
            {
                updateCmd.ExecuteNonQuery();
            }
        }

        public void Delete(SQLiteConnection connection, int id)
        {
            using (SQLiteCommand updateCmd =
                new SQLiteCommand($"DELETE FROM User WHERE Id = '{id}';", connection))
            {
                updateCmd.ExecuteNonQuery();
            }

        }
    }
}
