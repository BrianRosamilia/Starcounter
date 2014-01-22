using Codeplex.Data;
using Starcounter.Advanced;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Starcounter.Tools.Service.Task {
    internal class StatusTask {

        public static void Execute(StarcounterService service, out StatusEventArgs status) {

            string url = string.Format("{0}:{1}{2}", service.IPAddress, service.Port, "/api/server");

            Response response;
            X.GET(url, out response, null, 10000);

            if (response.IsSuccessStatusCode) {
                bool interactiveMode = false;

                try {
                    dynamic incomingJson = DynamicJson.Parse(response.Body);
                    bool bValid = incomingJson.IsDefined("Context");
                    if (bValid) {
                        string context = incomingJson.Context;
                        interactiveMode = context.Contains('@');
                    }
                }
                catch (Exception e) {
                    throw new Exception(e.ToString());
                }

                status = new StatusEventArgs() { Connected = true, InteractiveMode = interactiveMode };

            }
            else {
                status = new StatusEventArgs() { Connected = false };
            }
        }

    }
}
