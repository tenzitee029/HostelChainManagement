using QuanLyChuoiNhaTro.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyChuoiNhaTro.Services
{
    public static class UserSession
    {
        public static LoggedInUser? CurrentUser { get; private set; }

        public static void SignIn(LoggedInUser user)
        {
            CurrentUser = user;
        }

        public static void SignOut()
        {
            CurrentUser = null;
        }
    }
}
