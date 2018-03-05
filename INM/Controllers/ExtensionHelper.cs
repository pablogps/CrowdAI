using System;
using System.Web;
using INM.Controllers.Events;

namespace INM.Controllers
{
    public static class ExtensionHelper
    {
        public static string UserIdentity(this HttpContextBase context)
        {
            string userName;
            HttpCookie cookie = context.Request.Cookies["userNameCookie"];
            if (cookie == null)
            {
                userName = CookiesCounter.ReturnNewestID();
                cookie = new HttpCookie("userNameCookie", userName);
                cookie.Expires = DateTime.Now.AddDays(31);
                context.Response.Cookies.Add(cookie);
                System.Diagnostics.Debug.WriteLine("new user: " + userName);
                EventsController.WriteLineForDebug("Received a cookie!", userName);
            }
            else
            {
                userName = cookie.Value;
                CookiesCounter.EnsureLatestIdIsCurrent(userName);
            }
            return userName;
        }

        /*
        // To delete or modify cookies
        context.Response.Cookies.Remove("userNameCookie");
        cookie = new HttpCookie("userNameCookie", "Jacinto");
        cookie.Expires = DateTime.Now.AddDays(31);
        context.Response.Cookies.Add(cookie);
        */



    }
}