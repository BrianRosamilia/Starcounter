
using System.Collections.ObjectModel;
using Starcounter.InstallerEngine;

namespace Starcounter.InstallerWPF.Components
{
    public class VisualStudio2013Integration : BaseComponent
    {
        public const string Identifier = "VisualStudio2013Integration";

        public override string ComponentIdentifier
        {
            get
            {
                return VisualStudio2013Integration.Identifier;
            }
        }

        public override string Name
        {
            get
            {
                return "VisualStudio 2013 Integration";
            }
        }

        //public override bool IsExecuteCommandEnabled {
        //    get {
        //        return false;
        //    }
        //}

        public override bool CanBeInstalled {
            get {
                return true;
            }
        }
        //public override bool IsExecuteCommandEnabled {
        //    get {
        //        return false;
        //    }
        //}   


        private readonly string[] _Dependencys = new string[] { VisualStudio2013.Identifier, PersonalServer.Identifier };
        public override string[] Dependencys
        {
            get
            {
                return this._Dependencys;
            }
        }


        protected override void SetDefaultValues()
        {
            base.SetDefaultValues();

            this.IsInstalled = MainWindow.InstalledComponents[(int)ComponentsCheck.Components.VS2013Integration];

            switch (this.Command)
            {
                case ComponentCommand.Install:
                    this.ExecuteCommand = (!this.IsInstalled) && (DependenciesCheck.VStudio2013Installed());
                    break;
                case ComponentCommand.None:
                    this.ExecuteCommand = false;
                    break;
                case ComponentCommand.Uninstall:
                    this.ExecuteCommand = false;
                    break;
                case ComponentCommand.Update:
                    this.ExecuteCommand = false;
                    break;
            }
        }
        public VisualStudio2013Integration(ObservableCollection<BaseComponent> components)
            : base(components)
        {
        }

    }
}
