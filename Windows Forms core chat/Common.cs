using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Windows_Forms_CORE_CHAT_UGH
{
    public class Common
    {
        public const string C_USERNAME = "!username";
        public const string C_USER = "!user";
        public const string C_COMMANDS = "!commands";
        public const string C_WHO = "!who";
        public const string C_ABOUT = "!about";
        public const string C_WHISPER = "!whisper";
        public const string C_MOD = "!mod";
        public const string C_MODS = "!mods";
        public const string C_KICK   = "!kick";
        public const string C_EXIT   = "!exit";
        public const string C_TIMESTAMPS   = "!timestamps";
        public const string C_SCORES   = "!scores";
        public const string C_PASSWORD   = "!password";
        public const string C_SHOWPASSWORD   = "!showpassword";
        public const string C_LOGIN   = "!login";
        public const string SPACE   = " ";
        public const string DEFAULT_PASSWORD   = "1234";
        public static List<string> _commands
            = new List<string> { C_USERNAME, C_ABOUT, C_COMMANDS, C_WHO,
        C_MOD, C_KICK, C_USER, C_MODS, C_WHISPER, C_EXIT, C_TIMESTAMPS, C_SCORES, C_PASSWORD, C_SHOWPASSWORD, C_LOGIN};
    }
}
