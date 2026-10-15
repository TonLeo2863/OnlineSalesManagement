using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(OnlineSalesManagement.Startup))]
namespace OnlineSalesManagement
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
