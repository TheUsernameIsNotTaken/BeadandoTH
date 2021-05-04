using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataLibrary
{
    public class User
    {
        public string username;
        public string password;

        //Default constructor
        public User(string username, string password)
        {
            this.username = username;
            this.password = password;
        }

        public User()
        {
            username = null;
            password = null;
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(username) &&
                   !string.IsNullOrEmpty(password);
        }

        //Check user creditals
        public override bool Equals(object obj)
        {
            return obj is User user &&
                   IsValid() &&
                   user.IsValid() &&
                   username.Equals(user.username) &&
                   password.Equals(user.password);
        }

        public override int GetHashCode()
        {
            int hashCode = 1710835385;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(username);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(password);
            return hashCode;
        }

        public int SearchInList(List<User> list)
        {
            int i = 0;
            foreach (var element in list)
            {
                if (this.Equals(element)) { return i; }
                ++i;
            }
            return -1;
        }
    }
}
