using System;
using Starcounter;
using Starcounter.Server.PublicModel;
using Starcounter.Server.PublicModel.Commands;
using System.Web;

namespace Starcounter.Administrator {
    partial class DatabaseApp : Json {

        void Handle(Input.DatabaseName action) {
        }

        void Handle(Input.Start start) {

            DatabaseInfo database = Master.ServerInterface.GetDatabase(this.Uri);

            var startCMD = new StartDatabaseCommand(Master.ServerEngine, database.Name);
            CommandInfo cmdInfo = Master.ServerInterface.Execute(startCMD);
            Master.ServerInterface.Wait(cmdInfo);

            // Refresh info
            database = Master.ServerInterface.GetDatabase(this.Uri);
            this.SetDatabaseInfo(database);
        }

        void Handle(Input.Stop action) {

            DatabaseInfo database = Master.ServerInterface.GetDatabase(this.Uri);

            var cmd = new StopDatabaseCommand(Master.ServerEngine, database.Name, true);

            CommandInfo cmdInfo = Master.ServerInterface.Execute(cmd);
            Master.ServerInterface.Wait(cmdInfo);

            // Refresh info
            database = Master.ServerInterface.GetDatabase(this.Uri);
            this.SetDatabaseInfo(database);

        }

        public void SetDatabaseInfo(DatabaseInfo databaseInfo) {

            this.DatabaseName = databaseInfo.Name;

            this.DatabaseId = Master.EncodeTo64(databaseInfo.Uri);
            this.Uri = databaseInfo.Uri; 

            this.TransactionLogSize = (int)databaseInfo.TransactionLogSize;
            this.CollationFile = databaseInfo.CollationFile;
            this.SupportReplication = databaseInfo.SupportReplication;

            var engineInfo = databaseInfo.Engine;
            if (engineInfo == null || engineInfo.HostProcessId == 0) {
                this.HostProcessId = 0;
                this.Apps.Clear();
            } else {

                this.HostProcessId = engineInfo.HostProcessId;

                this.Apps.Clear(); // TODO: Update list, do not recreate it.
                foreach (var app in engineInfo.HostedApps) {
                    this.Apps.Add(new AppApp() { 
                        ExecutablePath = app.ExecutablePath,
                        WorkingDirectory = app.WorkingDirectory });
                }
            }
        }

    }
}

