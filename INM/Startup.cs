using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(INM.Startup))]
namespace INM
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
