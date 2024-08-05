using System;
using System.Threading.Tasks;
using System.Windows;

namespace Memoria.Launcher
{
    public sealed class UiLauncherAdvOptionsCloseButton : UiModManagerButton
    {
        public UiLauncherAdvOptionsCloseButton()
        {
            Label = "↩ Return";
        }

        protected override async Task DoAction()
        {
            await Task.Run(() =>
            {
                try
                {
                    ((Window)this.GetRootElement()).Close();
                }
                catch (Exception) { }
            });
        }
    }
}
