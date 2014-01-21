
using Starcounter.InstallerEngine;
using System;
using System.Collections.ObjectModel;

namespace Starcounter.InstallerWPF.Components
{
    public class VisualStudio2013 : BaseComponent
    {
        public const string Identifier = "VisualStudio 2013";

        public override bool CanBeInstalled
        {
            get
            {
                return true;
            }
        }
        public override string ComponentIdentifier
        {
            get
            {
                return VisualStudio2013.Identifier;
            }
        }

        public override string Name
        {
            get
            {
                return "VisualStudio 2013";
            }
        }

        protected override void SetDefaultValues()
        {
            base.SetDefaultValues();

            this.IsInstalled = DependenciesCheck.VStudio2013Installed();

        }

        public VisualStudio2013(ObservableCollection<BaseComponent> components)
            : base(components)
        {
        }

    }
}
