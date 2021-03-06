﻿
using Microsoft.VisualStudio.Shell;
using Starcounter.Internal;
using System;
using System.Diagnostics;

namespace Starcounter.VisualStudio {
    /// <summary>
    /// Represents an error task in the Error List window, originated
    /// from Starcounter extension code (e.g. deployment, debugging).
    /// </summary>
    internal class StarcounterErrorTask : ErrorTask {
        /// <summary>
        /// Possible help link.
        /// </summary>
        public string Helplink { get; set; }

        /// <summary>
        /// Gets a possible <see cref="ErrorMessage"/>.
        /// </summary>
        public ErrorMessage ErrorMessage { get; private set; }

        /// <summary>
        /// Gets the <see cref="VsPackage"/> to which this task
        /// belong.
        /// </summary>
        public VsPackage Package {
            get;
            set;
        }

        /// <summary>
        /// Gets the <see cref="ErrorTaskSource"/> of this task.
        /// </summary>
        public ErrorTaskSource Source {
            get {
                if (this.SubcategoryIndex == (int)ErrorTaskSource.Debug)
                    return ErrorTaskSource.Debug;
                else
                    return ErrorTaskSource.Other;
            }
        }

        public StarcounterErrorTask(string text, uint code = Error.SCERRUNSPECIFIED)
            : base() {
            BindToTextAndCode(text, code);
        }

        public StarcounterErrorTask(Exception e) : base(e) {
            ErrorMessage message;

            try {
                if (ErrorCode.TryGetCodedMessage(e, out message)) {
                    BindToErrorMessage(message);
                }
            } catch { }
        }

        public StarcounterErrorTask(ErrorMessage message) {
            BindToErrorMessage(message);
        }

        public void ShowInUserMessageWindow() {
            throw new NotImplementedException();
        }

        public void ShowInBrowser() {
            Process.Start(new ProcessStartInfo(GetHelplinkOrDefault()) {
                UseShellExecute = true,
                ErrorDialog = true
            });
        }

        protected override void OnNavigate(EventArgs e) {
            ShowInBrowser();
            base.OnNavigate(e);
        }

        protected override void OnHelp(EventArgs e) {
            ShowInBrowser();
            base.OnHelp(e);
        }

        /// <summary>
        /// Gets the value of the <see cref="Helplink"/> property or
        /// a link to a general troubleshooting page if the property
        /// is not assigned.
        /// </summary>
        /// <returns>The assigned help link value or the default.</returns>
        internal string GetHelplinkOrDefault() {
            // Come up with some good general troubleshooting links.
            // We should decide which to use based on the source of
            // this task, if a URL is not already set. The general
            // help page for deployment can discuss different issues
            // than the one for debugging, etc.
            // TODO:

            return string.IsNullOrEmpty(this.Helplink)
                ? StarcounterEnvironment.InternetAddresses.StarcounterWiki
                : this.Helplink;
        }

        private void BindToErrorMessage(ErrorMessage message) {
            this.Text = string.Format("{0} ({1}){2}{2}(Double-click here for additional help)", message.Body, message.Header, Environment.NewLine);
            this.Helplink = message.Helplink;
            this.ErrorMessage = message;
        }

        void BindToTextAndCode(string text, uint code) {
            this.Text = string.Format("{0}{1}{1}(Double-click here for additional help)", text, Environment.NewLine);
            this.Helplink = ErrorCode.ToHelpLink(code);
        }
    }
}
