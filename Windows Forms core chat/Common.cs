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
        public const string SPACE   = " ";
        public static List<string> _commands
            = new List<string> { Common.C_USERNAME, Common.C_ABOUT, Common.C_COMMANDS, Common.C_WHO,
        Common.C_MOD, Common.C_KICK, Common.C_USER, Common.C_MODS, Common.C_WHISPER, Common.C_EXIT, C_TIMESTAMPS};
    }
}
