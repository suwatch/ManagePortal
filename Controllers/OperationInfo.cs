using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace ManagePortal.Controllers
{
    public class OperationInfo
    {
        public string HttpMethod { get; set; }
        public JObject RequestBody { get; set; }
        public string Url { get; set; }
        [DefaultValue(false)]
        public bool RequireApiVersion { get; set; }
    }
}